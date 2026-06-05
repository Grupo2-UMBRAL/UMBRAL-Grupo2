using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Application.Scoring;
using Umbral.SessionOperations.Api.Application.SessionSnapshots;
using Umbral.SessionOperations.Api.Domain.LiveSessions;
using Umbral.SessionOperations.Api.Hubs;
using Umbral.SessionOperations.Api.Hubs.Contracts;
using Umbral.SessionOperations.Api.Infrastructure;

namespace Umbral.SessionOperations.Api.Application.Hints;

public sealed record ReleaseHintCommand(Guid LiveSessionId, Guid? SessionTeamId, Guid HintId)
    : IRequest<IReadOnlyList<VisibleHintSnapshot>>;

public sealed record ReleaseHintRequest(Guid? SessionTeamId);

public sealed class ReleaseHintHandler(
    SessionOperationsDbContext dbContext,
    TimeProvider timeProvider,
    IHubContext<SessionOperationsHub, ISessionClient> hubContext,
    IScoringAuditClient scoringAuditClient)
    : IRequestHandler<ReleaseHintCommand, IReadOnlyList<VisibleHintSnapshot>>
{
    public async Task<IReadOnlyList<VisibleHintSnapshot>> Handle(
        ReleaseHintCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await dbContext.LiveSessions
            .Include(session => session.SessionTeams)
            .Include(session => session.TeamProgressions)
            .Include(session => session.ReleasedHints)
            .SingleOrDefaultAsync(session => session.Id == request.LiveSessionId, cancellationToken);
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

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var releasedHint in releasedHints)
        {
            await LogHintReleasedAsync(releasedHint, cancellationToken);
        }

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
    {
        var payload = new HintUnlockedPayload(
            new RealtimeEventMetadata(
                liveSession.Id,
                liveSession.SequenceNumber,
                occurredAtUtc,
                SnapshotRefreshPolicy.ApplyIncremental,
                "Hint unlocked for Session Team."),
            sessionTeamId,
            visibleHint);

        await hubContext.Clients.All.ReceiveHintUnlocked(payload).WaitAsync(cancellationToken);
    }

    private async Task LogHintReleasedAsync(
        ReleasedHint releasedHint,
        CancellationToken cancellationToken)
        => await scoringAuditClient.LogSessionEventAsync(
            releasedHint.LiveSessionId,
            "HintReleased",
            $"Hint '{releasedHint.HintId}' released to Session Team '{releasedHint.SessionTeamId}' for Mission Stage '{releasedHint.MissionStageId}'. Reason: {releasedHint.UnlockReason}.",
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
