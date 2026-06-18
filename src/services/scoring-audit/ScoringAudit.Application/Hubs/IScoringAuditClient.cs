using ScoringAudit.Application.Features.Audit;
using ScoringAudit.Application.Features.Rankings;

namespace ScoringAudit.Application.Hubs;

public interface IScoringAuditClient
{
    Task ReceiveRankingUpdated(RankingPayload payload);

    Task ReceiveEventLogUpdated(SessionEventLogPayload payload);
}
