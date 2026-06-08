using Umbral.ScoringAudit.Api.Application.Audit;
using Umbral.ScoringAudit.Api.Application.Rankings;
using Umbral.ScoringAudit.Api.Domain.Audit;
using Umbral.ScoringAudit.Api.Domain.Scoreboards;

namespace Umbral.ScoringAudit.Api.Application.Scoreboards;

public interface IApplyPenaltyScoreboardStore
{
    Task<Scoreboard> LoadAsync(Guid liveSessionId, CancellationToken cancellationToken);

    Task PersistPenaltyApplicationAsync(SessionEventLog eventLog, CancellationToken cancellationToken);
}

public interface IScoringAuditUpdatesPublisher
{
    Task PublishRankingUpdatedAsync(RankingPayload ranking, CancellationToken cancellationToken);

    Task PublishEventLogUpdatedAsync(SessionEventLogPayload eventLog, CancellationToken cancellationToken);
}
