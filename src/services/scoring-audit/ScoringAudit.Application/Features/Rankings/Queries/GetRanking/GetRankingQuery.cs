namespace ScoringAudit.Application.Features.Rankings.Queries.GetRanking;

public sealed record GetRankingQuery(Guid LiveSessionId) : IRequest<RankingPayload>;
