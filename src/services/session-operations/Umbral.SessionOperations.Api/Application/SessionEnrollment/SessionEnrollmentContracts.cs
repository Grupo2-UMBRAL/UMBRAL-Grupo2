using Umbral.SessionOperations.Api.Domain.LiveSessions;

namespace Umbral.SessionOperations.Api.Application.SessionEnrollment;

public sealed record GenerateJoinCodeResponse(Guid LiveSessionId, string JoinCode);

public sealed record EnrollmentWindowResponse(
    Guid LiveSessionId,
    string? JoinCode,
    DateTimeOffset? OpenedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    bool IsOpen);

public sealed record RegisterTeamRequest(string JoinCode, string TeamName);

public sealed record RegisterTeamResponse(
    Guid LiveSessionId,
    Guid SessionTeamId,
    string TeamName,
    string ParticipantUserId,
    DateTimeOffset RegisteredAtUtc);

public interface IJoinCodeGenerator
{
    JoinCode Generate();
}

public interface ICurrentParticipantIdentity
{
    ParticipantUserId GetRequiredParticipantUserId();
}
