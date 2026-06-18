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

        ScoreEntry? scoreEntry = null;
        SessionEventLog? eventLog = null;
        var penaltyApplied = false;

        try
        {
            scoreEntry = scoreboard.ApplyPenalty(penalty);
            eventLog = new SessionEventLog(
                Guid.NewGuid(),
                request.LiveSessionId,
                "PenaltyApplied",
                CreatePenaltyAppliedDescription(penalty, scoreEntry),
                request.RecordedAt);
            await scoreboardStore.PersistPenaltyApplicationAsync(eventLog, cancellationToken);
            scoreboard.RebuildState();
            penaltyApplied = true;
        }
        catch (UmbralDomainException exception) when (exception.Code == "scoreboard.duplicate_penalty_command")
        {
            penaltyApplied = false;
        }

        var ranking = RankingProjection.Create(scoreboard, timeProvider.GetUtcNow());
        if (penaltyApplied)
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
            penaltyApplied ? penalty.PenaltyId : null,
            scoreEntry?.ScoreEntryId,
            penaltyApplied,
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
