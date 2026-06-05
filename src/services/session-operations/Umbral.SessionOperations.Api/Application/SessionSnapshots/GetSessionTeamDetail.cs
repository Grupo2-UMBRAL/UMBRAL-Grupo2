using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Domain.LiveSessions;
using Umbral.SessionOperations.Api.Infrastructure;

namespace Umbral.SessionOperations.Api.Application.SessionSnapshots;

public sealed record GetSessionTeamDetailQuery(
    Guid LiveSessionId,
    Guid SessionTeamId,
    int InactivityThresholdMinutes = 10) : IRequest<SessionTeamDetailResponse>;

public sealed record SessionTeamDetailResponse(
    Guid LiveSessionId,
    string LiveSessionName,
    string SessionState,
    Guid SessionTeamId,
    string TeamName,
    int ParticipantCount,
    string ProgressState,
    CurrentSessionStageSnapshot? CurrentStage,
    DateTimeOffset CurrentStageStartedAtUtc,
    IReadOnlyList<SessionTeamReleasedHintDetail> ReleasedHints,
    IReadOnlyList<SessionTeamEvidenceSubmissionDetail> EvidenceSubmissions,
    bool IsInactive,
    int InactivityThresholdMinutes,
    DateTimeOffset ServerTimeUtc,
    SnapshotSyncMetadata Sync);

public sealed record SessionTeamReleasedHintDetail(
    Guid ReleasedHintId,
    Guid HintId,
    Guid MissionStageId,
    string StageName,
    string Content,
    bool IsSolution,
    decimal? Latitude,
    decimal? Longitude,
    DateTimeOffset ReleasedAtUtc,
    string UnlockReason);

public sealed record SessionTeamEvidenceSubmissionDetail(
    Guid Id,
    Guid MissionStageId,
    string StageName,
    int SessionStageOrder,
    string Difficulty,
    string GameType,
    string? SubmittedHash,
    string? SubmittedText,
    string ValidationOutcome,
    string? FailureReason,
    DateTimeOffset SubmittedAtUtc,
    bool IsTriviaCorrectionEligible);

public sealed class GetSessionTeamDetailQueryHandler(
    SessionOperationsDbContext dbContext,
    TimeProvider timeProvider)
    : IRequestHandler<GetSessionTeamDetailQuery, SessionTeamDetailResponse>
{
    public async Task<SessionTeamDetailResponse> Handle(
        GetSessionTeamDetailQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await dbContext.LiveSessions
            .AsNoTracking()
            .AsSplitQuery()
            .Include(session => session.SessionTeams.Where(team => team.Id == request.SessionTeamId))
            .Include(session => session.TeamParticipations.Where(participation => participation.SessionTeamId == request.SessionTeamId))
            .Include(session => session.TeamProgressions.Where(progress => progress.SessionTeamId == request.SessionTeamId))
            .Include(session => session.EvidenceSubmissions.Where(submission => submission.SessionTeamId == request.SessionTeamId))
            .Include(session => session.ReleasedHints.Where(releasedHint => releasedHint.SessionTeamId == request.SessionTeamId))
            .SingleOrDefaultAsync(
                session => session.Id == request.LiveSessionId
                    && session.SessionTeams.Any(team => team.Id == request.SessionTeamId),
                cancellationToken);
        if (liveSession is null)
        {
            throw CreateSessionTeamNotFoundException(request.LiveSessionId, request.SessionTeamId);
        }

        var sessionTeam = liveSession.SessionTeams.SingleOrDefault(team => team.Id == request.SessionTeamId);
        if (sessionTeam is null)
        {
            throw CreateSessionTeamNotFoundException(request.LiveSessionId, request.SessionTeamId);
        }

        var serverTimeUtc = timeProvider.GetUtcNow();
        var progress = liveSession.TeamProgressions.FirstOrDefault(existingProgress =>
            existingProgress.SessionTeamId == request.SessionTeamId);
        var currentStage = liveSession.GetCurrentStageForTeam(request.SessionTeamId);
        var currentStageStartedAtUtc = GetCurrentStageStartedAtUtc(liveSession, progress);
        var submissions = liveSession.EvidenceSubmissions
            .Where(submission => submission.SessionTeamId == request.SessionTeamId)
            .OrderByDescending(submission => submission.SubmittedAtUtc)
            .ToArray();
        var releasedHints = liveSession.ReleasedHints
            .Where(releasedHint => releasedHint.SessionTeamId == request.SessionTeamId)
            .OrderByDescending(releasedHint => releasedHint.ReleasedAtUtc)
            .ToArray();
        var stagesById = liveSession.SessionStageFlow.ToDictionary(stage => stage.MissionStageId);

        return new SessionTeamDetailResponse(
            liveSession.Id,
            liveSession.Name,
            liveSession.State,
            sessionTeam.Id,
            sessionTeam.Name,
            liveSession.TeamParticipations.Count(participation => participation.SessionTeamId == sessionTeam.Id),
            liveSession.GetProgressStateForTeam(sessionTeam.Id),
            MapCurrentStage(currentStage),
            currentStageStartedAtUtc,
            MapReleasedHints(releasedHints, stagesById),
            MapEvidenceSubmissions(submissions, stagesById),
            CalculateIsInactive(submissions, currentStageStartedAtUtc, serverTimeUtc, request.InactivityThresholdMinutes),
            NormalizeInactivityThreshold(request.InactivityThresholdMinutes),
            serverTimeUtc,
            CreateSyncMetadata(liveSession, sessionTeam, progress, submissions, releasedHints, serverTimeUtc));
    }

    private static UmbralDomainException CreateSessionTeamNotFoundException(Guid liveSessionId, Guid sessionTeamId)
        => new(
            "session_team_not_found",
            $"Session Team '{sessionTeamId}' was not found in LiveSession '{liveSessionId}'.",
            UmbralFailureCategory.NotFound);

    private static DateTimeOffset GetCurrentStageStartedAtUtc(
        LiveSession liveSession,
        SessionTeamProgress? progress)
        => progress?.UpdatedAtUtc
            ?? liveSession.ScheduledStartAtUtc
            ?? liveSession.CreatedAtUtc;

    private static bool CalculateIsInactive(
        IReadOnlyCollection<EvidenceSubmission> submissions,
        DateTimeOffset currentStageStartedAtUtc,
        DateTimeOffset serverTimeUtc,
        int inactivityThresholdMinutes)
    {
        var latestSubmissionAtUtc = submissions
            .Select(submission => (DateTimeOffset?)submission.SubmittedAtUtc)
            .Max();
        var latestActivityAtUtc = latestSubmissionAtUtc ?? currentStageStartedAtUtc;

        return serverTimeUtc - latestActivityAtUtc > TimeSpan.FromMinutes(NormalizeInactivityThreshold(inactivityThresholdMinutes));
    }

    private static int NormalizeInactivityThreshold(int inactivityThresholdMinutes)
        => Math.Max(1, inactivityThresholdMinutes);

    private static SnapshotSyncMetadata CreateSyncMetadata(
        LiveSession liveSession,
        SessionTeam sessionTeam,
        SessionTeamProgress? progress,
        IReadOnlyCollection<EvidenceSubmission> submissions,
        IReadOnlyCollection<ReleasedHint> releasedHints,
        DateTimeOffset serverTimeUtc)
    {
        var lastUpdatedUtc = new[]
        {
            liveSession.CreatedAtUtc,
            sessionTeam.CreatedAtUtc,
            progress?.UpdatedAtUtc ?? sessionTeam.CreatedAtUtc,
            submissions
                .Select(submission => submission.SubmittedAtUtc)
                .DefaultIfEmpty(sessionTeam.CreatedAtUtc)
                .Max(),
            releasedHints
                .Select(releasedHint => releasedHint.ReleasedAtUtc)
                .DefaultIfEmpty(sessionTeam.CreatedAtUtc)
                .Max()
        }.Max();

        return new SnapshotSyncMetadata(
            liveSession.SequenceNumber,
            lastUpdatedUtc,
            serverTimeUtc);
    }

    private static CurrentSessionStageSnapshot? MapCurrentStage(LiveSessionStage? currentStage)
    {
        if (currentStage is null)
        {
            return null;
        }

        return new CurrentSessionStageSnapshot(
            currentStage.MissionStageId,
            currentStage.Name,
            currentStage.SessionStageOrder,
            currentStage.SourceOrder,
            currentStage.ResolvedTimeBudgetMinutes,
            currentStage.Difficulty,
            currentStage.GameType,
            currentStage.Prompt);
    }

    private static IReadOnlyList<SessionTeamReleasedHintDetail> MapReleasedHints(
        IReadOnlyCollection<ReleasedHint> releasedHints,
        IReadOnlyDictionary<Guid, LiveSessionStage> stagesById)
        => releasedHints
            .Select(releasedHint => MapReleasedHint(releasedHint, stagesById))
            .Where(detail => detail is not null)
            .Select(detail => detail!)
            .ToArray();

    private static SessionTeamReleasedHintDetail? MapReleasedHint(
        ReleasedHint releasedHint,
        IReadOnlyDictionary<Guid, LiveSessionStage> stagesById)
    {
        if (!stagesById.TryGetValue(releasedHint.MissionStageId, out var sessionStage))
        {
            return null;
        }

        var hint = sessionStage.Hints.FirstOrDefault(stageHint => stageHint.Id == releasedHint.HintId);
        if (hint is null)
        {
            return null;
        }

        return new SessionTeamReleasedHintDetail(
            releasedHint.Id,
            releasedHint.HintId,
            releasedHint.MissionStageId,
            sessionStage.Name,
            hint.Content,
            hint.IsSolution,
            hint.Latitude,
            hint.Longitude,
            releasedHint.ReleasedAtUtc,
            releasedHint.UnlockReason);
    }

    private static IReadOnlyList<SessionTeamEvidenceSubmissionDetail> MapEvidenceSubmissions(
        IReadOnlyCollection<EvidenceSubmission> submissions,
        IReadOnlyDictionary<Guid, LiveSessionStage> stagesById)
        => submissions
            .Select(submission => MapEvidenceSubmission(submission, stagesById))
            .ToArray();

    private static SessionTeamEvidenceSubmissionDetail MapEvidenceSubmission(
        EvidenceSubmission submission,
        IReadOnlyDictionary<Guid, LiveSessionStage> stagesById)
    {
        stagesById.TryGetValue(submission.MissionStageId, out var sessionStage);
        var gameType = sessionStage?.GameType ?? submission.GameType;
        var outcome = submission.Outcome.ToString();

        return new SessionTeamEvidenceSubmissionDetail(
            submission.Id,
            submission.MissionStageId,
            sessionStage?.Name ?? "Unknown Session Stage",
            sessionStage?.SessionStageOrder ?? 0,
            sessionStage?.Difficulty ?? "Unknown",
            gameType,
            submission.SubmittedHash,
            submission.SubmittedText,
            outcome,
            submission.FailureReason,
            submission.SubmittedAtUtc,
            IsTriviaCorrectionEligible(gameType, submission.Outcome));
    }

    private static bool IsTriviaCorrectionEligible(string gameType, ValidationOutcome outcome)
        => string.Equals(gameType, "Trivia", StringComparison.OrdinalIgnoreCase)
            && outcome == ValidationOutcome.Rejected;
}
