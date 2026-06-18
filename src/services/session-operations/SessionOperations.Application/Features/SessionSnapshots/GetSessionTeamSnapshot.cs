using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using SessionOperations.Application.Features.SessionEnrollment;
using SessionOperations.Domain.LiveSessions;
using SessionOperations.Application.Abstractions;

namespace SessionOperations.Application.Features.SessionSnapshots;

public sealed record GetSessionTeamSnapshotQuery(Guid SessionTeamId) : IRequest<SessionTeamSnapshot>;

public sealed class GetSessionTeamSnapshotQueryHandler(
    ISessionOperationsDbContext dbContext,
    ICurrentParticipantIdentity currentParticipantIdentity,
    TimeProvider timeProvider)
    : IRequestHandler<GetSessionTeamSnapshotQuery, SessionTeamSnapshot>
{
    public async Task<SessionTeamSnapshot> Handle(
        GetSessionTeamSnapshotQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var participantUserId = currentParticipantIdentity.GetRequiredParticipantUserId();
        var liveSession = await dbContext.LiveSessions
            .AsNoTracking()
            .Include(session => session.SessionTeams)
            .Include(session => session.TeamParticipations)
            .Include(session => session.TeamProgressions)
            .Include(session => session.EvidenceSubmissions)
            .Include(session => session.ReleasedHints)
            .SingleOrDefaultAsync(
                session => session.SessionTeams.Any(team => team.Id == request.SessionTeamId),
                cancellationToken);
        if (liveSession is null)
        {
            throw CreateSessionTeamNotFoundException(request.SessionTeamId);
        }

        var sessionTeam = liveSession.SessionTeams.Single(team => team.Id == request.SessionTeamId);
        var participation = liveSession.TeamParticipations.FirstOrDefault(existingParticipation =>
            existingParticipation.SessionTeamId == sessionTeam.Id
            && string.Equals(
                existingParticipation.ParticipantUserId,
                participantUserId.Value,
                StringComparison.Ordinal));
        if (participation is null)
        {
            throw new UmbralDomainException(
                "session_team_participation_required",
                "Participant must belong to this Session Team to read its snapshot.",
                UmbralFailureCategory.Forbidden);
        }

        var serverTimeUtc = timeProvider.GetUtcNow();
        return new SessionTeamSnapshot(
            liveSession.Id,
            sessionTeam.Id,
            sessionTeam.Name,
            liveSession.State,
            liveSession.GetProgressStateForTeam(sessionTeam.Id),
            MapCurrentStage(liveSession.GetCurrentStageForTeam(sessionTeam.Id)),
            MapVisibleHints(liveSession, sessionTeam.Id),
            CreateSyncMetadata(liveSession, sessionTeam, serverTimeUtc),
            MapAllStages(liveSession));
    }

    private static UmbralDomainException CreateSessionTeamNotFoundException(Guid sessionTeamId)
        => new(
            "session_team_not_found",
            $"Session Team '{sessionTeamId}' was not found.",
            UmbralFailureCategory.NotFound);

    private static SnapshotSyncMetadata CreateSyncMetadata(
        LiveSession liveSession,
        SessionTeam sessionTeam,
        DateTimeOffset serverTimeUtc)
    {
        var teamLastParticipationAtUtc = liveSession.TeamParticipations
            .Where(participation => participation.SessionTeamId == sessionTeam.Id)
            .Select(participation => participation.EnrolledAtUtc)
            .DefaultIfEmpty(sessionTeam.CreatedAtUtc)
            .Max();
        var teamProgressUpdatedAtUtc = liveSession.TeamProgressions
            .Where(progress => progress.SessionTeamId == sessionTeam.Id)
            .Select(progress => progress.UpdatedAtUtc)
            .DefaultIfEmpty(sessionTeam.CreatedAtUtc)
            .Max();
        var teamLastSubmissionAtUtc = liveSession.EvidenceSubmissions
            .Where(submission => submission.SessionTeamId == sessionTeam.Id)
            .Select(submission => submission.SubmittedAtUtc)
            .DefaultIfEmpty(sessionTeam.CreatedAtUtc)
            .Max();
        var teamLastHintReleasedAtUtc = liveSession.ReleasedHints
            .Where(releasedHint => releasedHint.SessionTeamId == sessionTeam.Id)
            .Select(releasedHint => releasedHint.ReleasedAtUtc)
            .DefaultIfEmpty(sessionTeam.CreatedAtUtc)
            .Max();
        var lastUpdatedUtc = new[]
        {
            liveSession.CreatedAtUtc,
            sessionTeam.CreatedAtUtc,
            teamLastParticipationAtUtc,
            teamProgressUpdatedAtUtc,
            teamLastSubmissionAtUtc,
            teamLastHintReleasedAtUtc
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

    private static IReadOnlyList<CurrentSessionStageSnapshot>? MapAllStages(LiveSession liveSession)
    {
        if (!string.Equals(liveSession.State, LiveSessionStates.Finalized, StringComparison.Ordinal))
        {
            return null;
        }

        return liveSession.SessionStageFlow
            .OrderBy(stage => stage.SessionStageOrder)
            .Select(stage => MapCurrentStage(stage)!)
            .ToArray();
    }

    private static IReadOnlyList<VisibleHintSnapshot> MapVisibleHints(
        LiveSession liveSession,
        Guid sessionTeamId)
    {
        var sessionStagesById = liveSession.SessionStageFlow.ToDictionary(stage => stage.MissionStageId);
        var visibleHints = new List<VisibleHintSnapshot>();

        foreach (var releasedHint in liveSession.ReleasedHints
            .Where(releasedHint => releasedHint.SessionTeamId == sessionTeamId)
            .OrderBy(releasedHint => releasedHint.ReleasedAtUtc))
        {
            if (!sessionStagesById.TryGetValue(releasedHint.MissionStageId, out var sessionStage))
            {
                continue;
            }

            var hint = sessionStage.Hints.FirstOrDefault(stageHint => stageHint.Id == releasedHint.HintId);
            if (hint is null)
            {
                continue;
            }

            if (hint.IsSolution
                && !string.Equals(liveSession.State, LiveSessionStates.Finalized, StringComparison.Ordinal))
            {
                continue;
            }

            visibleHints.Add(new VisibleHintSnapshot(
                hint.Id,
                sessionStage.MissionStageId,
                hint.Content,
                hint.IsSolution,
                hint.Latitude,
                hint.Longitude,
                releasedHint.ReleasedAtUtc,
                releasedHint.UnlockReason));
        }

        return visibleHints;
    }
}
