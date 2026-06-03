using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Domain.LiveSessions;
using Umbral.SessionOperations.Api.Infrastructure;

namespace Umbral.SessionOperations.Api.Application.SessionSnapshots;

public sealed record GetLiveSessionOverviewQuery(Guid LiveSessionId) : IRequest<LiveSessionOverview>;

public sealed class GetLiveSessionOverviewQueryHandler(
    SessionOperationsDbContext dbContext,
    TimeProvider timeProvider)
    : IRequestHandler<GetLiveSessionOverviewQuery, LiveSessionOverview>
{
    public async Task<LiveSessionOverview> Handle(
        GetLiveSessionOverviewQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await dbContext.LiveSessions
            .AsNoTracking()
            .Include(session => session.SessionTeams)
            .Include(session => session.TeamParticipations)
            .SingleOrDefaultAsync(session => session.Id == request.LiveSessionId, cancellationToken);
        if (liveSession is null)
        {
            throw CreateLiveSessionNotFoundException(request.LiveSessionId);
        }

        var serverTimeUtc = timeProvider.GetUtcNow();
        var participantCounts = liveSession.TeamParticipations
            .GroupBy(participation => participation.SessionTeamId)
            .ToDictionary(group => group.Key, group => group.Count());
        var currentStage = SelectCurrentStage(liveSession.SessionStageFlow);
        var sessionTeams = liveSession.SessionTeams
            .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
            .Select(team => new LiveSessionOverviewTeam(
                team.Id,
                team.Name,
                participantCounts.GetValueOrDefault(team.Id),
                SessionSnapshotConstants.NotStartedProgressState,
                currentStage))
            .ToArray();

        return new LiveSessionOverview(
            liveSession.Id,
            liveSession.Name,
            liveSession.MissionId,
            liveSession.MissionName,
            liveSession.State,
            liveSession.ScheduledStartAtUtc,
            CalculateRemainingSeconds(liveSession, serverTimeUtc),
            serverTimeUtc,
            CreateSyncMetadata(liveSession, serverTimeUtc),
            sessionTeams);
    }

    private static UmbralDomainException CreateLiveSessionNotFoundException(Guid liveSessionId)
        => new(
            "live_session_not_found",
            $"LiveSession '{liveSessionId}' was not found.",
            UmbralFailureCategory.NotFound);

    private static int? CalculateRemainingSeconds(LiveSession liveSession, DateTimeOffset serverTimeUtc)
    {
        if (!string.Equals(liveSession.State, LiveSessionStates.Scheduled, StringComparison.Ordinal))
        {
            return null;
        }

        if (liveSession.ScheduledStartAtUtc is null)
        {
            return null;
        }

        var remaining = liveSession.ScheduledStartAtUtc.Value - serverTimeUtc;
        if (remaining <= TimeSpan.Zero)
        {
            return 0;
        }

        return (int)Math.Ceiling(remaining.TotalSeconds);
    }

    private static SnapshotSyncMetadata CreateSyncMetadata(LiveSession liveSession, DateTimeOffset serverTimeUtc)
    {
        var lastUpdatedUtc = new[]
            {
                liveSession.CreatedAtUtc,
                liveSession.SessionTeams.Select(team => team.CreatedAtUtc).DefaultIfEmpty(liveSession.CreatedAtUtc).Max(),
                liveSession.TeamParticipations
                    .Select(participation => participation.EnrolledAtUtc)
                    .DefaultIfEmpty(liveSession.CreatedAtUtc)
                    .Max()
            }
            .Max();

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
