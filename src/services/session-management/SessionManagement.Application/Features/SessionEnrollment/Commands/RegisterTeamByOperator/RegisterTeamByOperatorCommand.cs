using MediatR;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed record RegisterTeamByOperatorRequest(string TeamName);

public sealed record RegisterTeamByOperatorCommand(Guid LiveSessionId, string TeamName)
    : IRequest<RegisterTeamByOperatorResponse>;

public sealed record RegisterTeamByOperatorResponse(
    Guid LiveSessionId,
    Guid SessionTeamId,
    string TeamName,
    DateTimeOffset CreatedAtUtc);
