using MediatR;

using System.Text.Json.Serialization;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed record RegisterTeamByOperatorRequest([property: JsonPropertyName("teamName")] string TeamName);

public sealed record RegisterTeamByOperatorCommand(Guid LiveSessionId, string TeamName)
    : IRequest<RegisterTeamByOperatorResponse>;

public sealed record RegisterTeamByOperatorResponse(
    Guid LiveSessionId,
    Guid SessionTeamId,
    string TeamName,
    DateTimeOffset CreatedAtUtc);
