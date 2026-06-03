using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Application.SessionEnrollment;
using Umbral.SessionOperations.Api.Domain.LiveSessions;
using Umbral.SessionOperations.Api.Infrastructure;

namespace Umbral.SessionOperations.Api.Application.SessionSnapshots;

public sealed record GetSessionTeamSnapshotQuery(Guid SessionTeamId) : IRequest<SessionTeamSnapshot>;

public sealed class GetSessionTeamSnapshotQueryHandler(
    SessionOperationsDbContext dbContext,
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
            SessionSnapshotConstants.NotStartedProgressState,
            SelectCurrentStage(liveSession.SessionStageFlow),
            Array.Empty<VisibleHintSnapshot>(),
            CreateSyncMetadata(liveSession, sessionTeam, serverTimeUtc));
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
        var lastUpdatedUtc = new[]
        {
            liveSession.CreatedAtUtc,
            sessionTeam.CreatedAtUtc,
            teamLastParticipationAtUtc
        }.Max();

        return new SnapshotSyncMetadata(
            SessionSnapshotConstants.InitialSequenceNumber,
            lastUpdatedUtc,
            serverTimeUtc);
    }

    private static CurrentSessionStageSnapshot? SelectCurrentStage(IReadOnlyList<LiveSessionStage> sessionStageFlow)
    {
        var currentStage = sessionStageFlow
            .OrderBy(stage => stage.SessionStageOrder)
            .FirstOrDefault();
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
            currentStage.GameType);
    }
}
