using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed record GenerateJoinCodeResponse(Guid LiveSessionId, string JoinCode);

public sealed record EnrollmentWindowResponse(
    Guid LiveSessionId,
    string? JoinCode,
    DateTimeOffset? OpenedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    bool IsOpen);

public sealed record ParticipantEnrollmentStatusResponse(
    Guid LiveSessionId,
    string SessionState,
    DateTimeOffset? OpenedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    bool IsOpen);

public sealed record SessionTeamResponse(Guid Id, string Name);

public sealed record SessionTeamsResponse(
    Guid LiveSessionId,
    bool IsOpen,
    IReadOnlyList<SessionTeamResponse> Teams);

public sealed record RegisterTeamRequest(string JoinCode, string TeamName);

public sealed record RegisterTeamResponse(
    Guid LiveSessionId,
    Guid SessionTeamId,
    string TeamName,
    string ParticipantUserId,
    DateTimeOffset RegisteredAtUtc);

public sealed record JoinSessionTeamRequest(string JoinCode, Guid SessionTeamId);

public sealed record JoinSessionTeamResponse(
    Guid LiveSessionId,
    Guid SessionTeamId,
    string TeamName,
    string ParticipantUserId,
    DateTimeOffset EnrolledAtUtc);

public interface IJoinCodeGenerator
{
    JoinCode Generate();
}

public interface ICurrentParticipantIdentity
{
    ParticipantUserId GetRequiredParticipantUserId();
}
