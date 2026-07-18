using SessionManagement.Domain.LiveSessions;
using MediatR;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Abstractions;
using SessionManagement.Domain.LiveSessions.States;

namespace SessionManagement.Application.Features.SessionSnapshots;

public sealed class GetLiveSessionOverviewQueryHandler(
    ILiveSessionReadRepository liveSessionRepository,
    TimeProvider timeProvider)
    : IRequestHandler<GetLiveSessionOverviewQuery, LiveSessionOverview>
{
    public async Task<LiveSessionOverview> Handle(
        GetLiveSessionOverviewQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await liveSessionRepository.GetOverviewAsync(request.LiveSessionId, cancellationToken);
        if (liveSession is null)
        {
            throw CreateLiveSessionNotFoundException(request.LiveSessionId);
        }

        var serverTimeUtc = timeProvider.GetUtcNow();
        var participantCounts = liveSession.TeamParticipations
            .GroupBy(participation => participation.SessionTeamId)
            .ToDictionary(group => group.Key, group => group.Count());
        var sessionTeams = liveSession.SessionTeams
            .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
            .Select(team => new LiveSessionOverviewTeam(
                team.Id,
                team.Name,
                participantCounts.GetValueOrDefault(team.Id),
                liveSession.GetProgressStateForTeam(team.Id),
                MapCurrentStage(liveSession.GetCurrentStageForTeam(team.Id)),
                MapVisibleHints(liveSession, team.Id)))
            .ToArray();

        return new LiveSessionOverview(
            liveSession.Id,
            liveSession.Name,
            liveSession.MissionId,
            liveSession.MissionName,
            liveSession.State.Name,
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
        if (liveSession.State is not ScheduledState)
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
                    .Max(),
                liveSession.TeamProgressions
                    .Select(progress => progress.UpdatedAtUtc)
                    .DefaultIfEmpty(liveSession.CreatedAtUtc)
                    .Max(),
                liveSession.EvidenceSubmissions
                    .Select(submission => submission.SubmittedAtUtc)
                    .DefaultIfEmpty(liveSession.CreatedAtUtc)
                    .Max(),
                liveSession.ReleasedHints
                    .Select(releasedHint => releasedHint.ReleasedAtUtc)
                    .DefaultIfEmpty(liveSession.CreatedAtUtc)
                    .Max()
            }
            .Max();

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




