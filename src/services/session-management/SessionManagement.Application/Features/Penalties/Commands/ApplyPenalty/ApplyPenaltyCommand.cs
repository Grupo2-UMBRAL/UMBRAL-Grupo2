using MediatR;
using SessionManagement.Application.Scoring;

namespace SessionManagement.Application.Features.Penalties;

public sealed record ApplyPenaltyCommand(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid CommandId,
    string Severity,
    string Reason) : IRequest<ApplyPenaltyResponse>;

public sealed record ApplyPenaltyRequest(
    Guid SessionTeamId,
    Guid CommandId,
    string Severity,
    string Reason);

public sealed record ApplyPenaltyResponse(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid CommandId,
    Guid? PenaltyId,
    Guid? ScoreEntryId,
    bool PenaltyApplied,
    int VisibleScore,
    RankingPayload Ranking,
    DateTimeOffset RecordedAtUtc);

