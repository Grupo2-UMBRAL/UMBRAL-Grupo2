using ScoringMonitoring.Application.Features.SessionEventLogs;
using ScoringMonitoring.Application.Features.Rankings;
using ScoringMonitoring.Domain.Audit;
using ScoringMonitoring.Domain.Penalties;
using ScoringMonitoring.Domain.Scoreboards;

namespace ScoringMonitoring.Application.Features.Scoreboards.Commands.ApplyPenalty;

public sealed class ApplyPenaltyHandler(
    IApplyPenaltyScoreboardStore scoreboardStore,
    TimeProvider timeProvider,
    IScoringMonitoringUpdatesPublisher updatesPublisher)
    : IRequestHandler<ApplyPenaltyCommand, ApplyPenaltyResponse>
{
    public async Task<ApplyPenaltyResponse> Handle(
        ApplyPenaltyCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scoreboard = await scoreboardStore.LoadAsync(request.LiveSessionId, cancellationToken);

        var penalty = new Penalty(
            Guid.NewGuid(),
            request.CommandId,
            request.SessionTeamId,
            ParseSeverity(request.Severity),
            request.AppliedByOperatorUserId,
            request.Reason,
            request.RecordedAt);

        var scoreEntry = scoreboard.ApplyPenalty(penalty);
        SessionEventLog? eventLog = null;
        if (scoreEntry is not null)
        {
            eventLog = new SessionEventLog(
                Guid.NewGuid(),
                request.LiveSessionId,
                "PenaltyApplied",
                CreatePenaltyAppliedDescription(penalty, scoreEntry),
                request.RecordedAt);
            await scoreboardStore.PersistPenaltyApplicationAsync(eventLog, cancellationToken);
            scoreboard.RebuildState();
        }

        var ranking = RankingProjection.Create(scoreboard, timeProvider.GetUtcNow());
        if (scoreEntry is not null)
        {
            await updatesPublisher.PublishRankingUpdatedAsync(ranking, cancellationToken);
            await updatesPublisher.PublishEventLogUpdatedAsync(
                SessionEventLogPayload.FromEntity(eventLog!),
                cancellationToken);
        }

        return new ApplyPenaltyResponse(
            scoreboard.LiveSessionId,
            request.SessionTeamId,
            request.CommandId,
            scoreEntry is not null ? penalty.PenaltyId : null,
            scoreEntry?.ScoreEntryId,
            scoreEntry is not null,
            scoreboard.GetTeamScore(request.SessionTeamId).VisibleScore,
            ranking);
    }

    private static PenaltySeverity ParseSeverity(string severity)
    {
        if (Enum.TryParse<PenaltySeverity>(severity, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new UmbralDomainException(
            "penalty.unknown_severity",
            "Penalty Severity is not supported.",
            UmbralFailureCategory.Validation);
    }

    private static string CreatePenaltyAppliedDescription(Penalty penalty, ScoreEntry scoreEntry)
    {
        var reason = penalty.Reason.EndsWith(".", StringComparison.Ordinal)
            ? penalty.Reason
            : $"{penalty.Reason}.";

        return $"Penalty of severity '{penalty.Severity}' applied to Session Team '{penalty.SessionTeamId}' by Operator '{penalty.AppliedByOperatorUserId}' for reason: {reason} Score variation: {scoreEntry.Delta} points.";
    }
}

