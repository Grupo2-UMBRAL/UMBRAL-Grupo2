using MediatR;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Abstractions.Scoring;
using SessionManagement.Application.Abstractions.Realtime;
using SessionManagement.Application.Features.SessionSnapshots;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.Hints;

public sealed class ReleaseHintHandler(
    ILiveSessionRepository liveSessionRepository,
    TimeProvider timeProvider,
    ISessionRealtimeNotifier realtimeNotifier)
    : IRequestHandler<ReleaseHintCommand, IReadOnlyList<VisibleHintSnapshot>>
{
    public async Task<IReadOnlyList<VisibleHintSnapshot>> Handle(
        ReleaseHintCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await liveSessionRepository.GetForHintReleaseAsync(request.LiveSessionId, cancellationToken);
        if (liveSession is null)
        {
            throw new UmbralDomainException(
                "live_session_not_found",
                $"LiveSession '{request.LiveSessionId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        var releasedAtUtc = timeProvider.GetUtcNow();
        var releasedHints = request.SessionTeamId is { } sessionTeamId && sessionTeamId != Guid.Empty
            ? ReleaseForSingleTeam(liveSession, sessionTeamId, request.HintId, releasedAtUtc)
            : ReleaseForEligibleTeams(liveSession, request.HintId, releasedAtUtc);

        await liveSessionRepository.SaveChangesAsync(cancellationToken);

        var visibleHints = releasedHints
            .Select(releasedHint => MapVisibleHint(liveSession, releasedHint))
            .ToArray();

        foreach (var releasedHint in releasedHints)
        {
            await PublishHintUnlockedAsync(
                liveSession,
                releasedHint.SessionTeamId,
            MapVisibleHint(liveSession, releasedHint),
            releasedAtUtc,
            cancellationToken);
        }

        return visibleHints;
    }

    private static IReadOnlyList<ReleasedHint> ReleaseForSingleTeam(
        LiveSession liveSession,
        Guid sessionTeamId,
        Guid hintId,
        DateTimeOffset releasedAtUtc)
        => new[] { liveSession.ReleaseHint(sessionTeamId, hintId, releasedAtUtc) };

    private static IReadOnlyList<ReleasedHint> ReleaseForEligibleTeams(
        LiveSession liveSession,
        Guid hintId,
        DateTimeOffset releasedAtUtc)
    {
        var hintStage = liveSession.SessionStageFlow.FirstOrDefault(stage => stage.Hints.Any(hint => hint.Id == hintId));
        if (hintStage is null)
        {
            throw new UmbralDomainException(
                "released_hint_not_found",
                $"Hint '{hintId}' was not found in this LiveSession flow.",
                UmbralFailureCategory.NotFound);
        }

        var releasedHints = new List<ReleasedHint>();
        foreach (var sessionTeam in liveSession.SessionTeams.OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase))
        {
            var currentStage = liveSession.GetCurrentStageForTeam(sessionTeam.Id);
            if (currentStage?.MissionStageId != hintStage.MissionStageId)
            {
                continue;
            }

            if (liveSession.ReleasedHints.Any(releasedHint =>
                releasedHint.SessionTeamId == sessionTeam.Id
                && releasedHint.MissionStageId == hintStage.MissionStageId
                && releasedHint.HintId == hintId))
            {
                continue;
            }

            releasedHints.Add(liveSession.ReleaseHint(sessionTeam.Id, hintId, releasedAtUtc));
        }

        if (releasedHints.Count > 0)
        {
            return releasedHints;
        }

        throw new UmbralDomainException(
            "released_hint_no_eligible_session_teams",
            "No eligible Session Teams can receive this Hint.",
            UmbralFailureCategory.Conflict);
    }

    private async Task PublishHintUnlockedAsync(
        LiveSession liveSession,
        Guid sessionTeamId,
        VisibleHintSnapshot visibleHint,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
        => await realtimeNotifier.NotifyHintUnlockedAsync(
            new HintUnlockedPayload(
                new RealtimeEventMetadata(
                    liveSession.Id,
                    liveSession.SequenceNumber,
                    occurredAtUtc,
                    SnapshotRefreshPolicy.ApplyIncremental,
                    "Hint unlocked for Session Team."),
                sessionTeamId,
                visibleHint),
            cancellationToken);

    private static VisibleHintSnapshot MapVisibleHint(LiveSession liveSession, ReleasedHint releasedHint)
    {
        var stage = liveSession.SessionStageFlow.Single(sessionStage =>
            sessionStage.MissionStageId == releasedHint.MissionStageId);
        var hint = stage.Hints.Single(stageHint => stageHint.Id == releasedHint.HintId);

        return new VisibleHintSnapshot(
            hint.Id,
            stage.MissionStageId,
            hint.Content,
            hint.IsSolution,
            hint.Latitude,
            hint.Longitude,
            releasedHint.ReleasedAtUtc,
            releasedHint.UnlockReason);
    }
}



