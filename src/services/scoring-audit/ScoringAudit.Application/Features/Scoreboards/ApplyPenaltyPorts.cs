using ScoringAudit.Application.Features.Audit;
using ScoringAudit.Application.Features.Rankings;
using ScoringAudit.Domain.Audit;
using ScoringAudit.Domain.Scoreboards;

namespace ScoringAudit.Application.Features.Scoreboards;

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
