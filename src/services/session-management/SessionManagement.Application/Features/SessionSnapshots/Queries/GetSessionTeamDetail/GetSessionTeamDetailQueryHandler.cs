using SessionManagement.Domain.LiveSessions;
using MediatR;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.SessionSnapshots;

public sealed class GetSessionTeamDetailQueryHandler(
    ILiveSessionReadRepository liveSessionRepository,
    TimeProvider timeProvider)
    : IRequestHandler<GetSessionTeamDetailQuery, SessionTeamDetailResponse>
{
    public async Task<SessionTeamDetailResponse> Handle(
        GetSessionTeamDetailQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await liveSessionRepository.GetSessionTeamDetailAsync(request.LiveSessionId, request.SessionTeamId, cancellationToken);
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
            liveSession.State.Value,
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
            currentStage.Prompt,
            currentStage.Choices.Select(choice => new SessionStageChoiceSnapshot(choice.Id, choice.Text)).ToArray());
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
            submission.SubmittedChoiceId,
            outcome,
            submission.FailureReason,
            submission.SubmittedAtUtc,
            IsTriviaCorrectionEligible(gameType, submission.Outcome));
    }

    private static bool IsTriviaCorrectionEligible(string gameType, ValidationOutcome outcome)
        => string.Equals(gameType, "Trivia", StringComparison.OrdinalIgnoreCase)
            && outcome == ValidationOutcome.Rejected;
}




