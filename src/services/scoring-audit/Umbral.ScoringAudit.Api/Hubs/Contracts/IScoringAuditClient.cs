using Umbral.ScoringAudit.Api.Application.Rankings;

namespace Umbral.ScoringAudit.Api.Hubs.Contracts;

public interface IScoringAuditClient
{
    Task ReceiveRankingUpdated(RankingPayload payload);
}
