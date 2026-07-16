using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.Application.Features.SessionEnrollment;

/// <summary>
/// The Session Join Code an operator hands out so participants can reach a LiveSession.
/// </summary>
/// <param name="LiveSessionId" example="5e1d9f34-8b26-4a7c-9f10-3c2b5d6e7a80">Session the code points to.</param>
/// <param name="JoinCode" example="H7KP2M">Six characters from an unambiguous alphabet (no I, O, 0 or 1) and always uppercase. It identifies the session only; it does not pick a Session Team.</param>
public sealed record GenerateJoinCodeResponse(Guid LiveSessionId, string JoinCode);

/// <summary>
/// Operator-facing state of the Team Assignment Window after opening or closing it.
/// </summary>
/// <param name="LiveSessionId" example="5e1d9f34-8b26-4a7c-9f10-3c2b5d6e7a80">Session whose window was changed.</param>
/// <param name="JoinCode" example="H7KP2M">Session Join Code in force. null = never generated, so participants have no way in even while the window is open.</param>
/// <param name="OpenedAtUtc" example="2026-07-16T20:45:00Z">Instant the window opened. null = it was never opened.</param>
/// <param name="ClosedAtUtc" example="2026-07-16T21:00:00Z">Instant the window closed. null = it was never closed.</param>
/// <param name="IsOpen">true = participants may still create or change their Session Team. Once false, any reassignment is an exceptional operator intervention.</param>
public sealed record EnrollmentWindowResponse(
    Guid LiveSessionId,
    string? JoinCode,
    DateTimeOffset? OpenedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    bool IsOpen);

/// <summary>
/// What a participant learns after validating a Session Join Code: which session it opens and
/// whether that session is currently accepting team assignments.
/// </summary>
/// <param name="LiveSessionId" example="5e1d9f34-8b26-4a7c-9f10-3c2b5d6e7a80">Session the validated code resolves to.</param>
/// <param name="SessionState" example="Scheduled">Session State: Scheduled, Active, Paused, Finalized or Canceled.</param>
/// <param name="OpenedAtUtc" example="2026-07-16T20:45:00Z">Instant the Team Assignment Window opened. null = it was never opened.</param>
/// <param name="ClosedAtUtc" example="2026-07-16T21:00:00Z">Instant the Team Assignment Window closed. null = it was never closed.</param>
/// <param name="IsOpen">true = the participant may still create or join a Session Team.</param>
public sealed record ParticipantEnrollmentStatusResponse(
    Guid LiveSessionId,
    string SessionState,
    DateTimeOffset? OpenedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    bool IsOpen);

/// <summary>
/// A Session Team offered to a participant who is choosing which group to play with.
/// </summary>
/// <param name="Id" example="6f2e5d84-1a7b-4c39-9e02-5b8d3f7a1c46">Value the participant sends back to join this team. Valid only inside its LiveSession.</param>
/// <param name="Name" example="Los Topos">Name the creating participant chose, used to recognise the group in the lobby.</param>
public sealed record SessionTeamResponse(Guid Id, string Name);

/// <summary>
/// The Session Teams a participant can join with a valid Session Join Code.
/// </summary>
/// <param name="LiveSessionId" example="5e1d9f34-8b26-4a7c-9f10-3c2b5d6e7a80">Session the listed teams belong to.</param>
/// <param name="IsOpen">true = the Team Assignment Window still admits joining or creating a team. When false the list is informational only.</param>
/// <param name="Teams">Teams registered so far. Empty means nobody has created one yet, so the participant must create the first.</param>
public sealed record SessionTeamsResponse(
    Guid LiveSessionId,
    bool IsOpen,
    IReadOnlyList<SessionTeamResponse> Teams);

/// <summary>
/// Data a participant sends to create a Session Team and be enrolled into it.
/// </summary>
/// <param name="JoinCode" example="H7KP2M">Session Join Code that selects the target LiveSession. Case-insensitive; six characters.</param>
/// <param name="TeamName" example="Los Topos">Name for the new team, shown to other participants choosing a group.</param>
public sealed record RegisterTeamRequest(string JoinCode, string TeamName);

/// <summary>
/// Result of creating a Session Team: the team exists and the calling participant already plays in it.
/// </summary>
/// <param name="LiveSessionId" example="5e1d9f34-8b26-4a7c-9f10-3c2b5d6e7a80">Session the code resolved to.</param>
/// <param name="SessionTeamId" example="6f2e5d84-1a7b-4c39-9e02-5b8d3f7a1c46">The new team. Addresses this team's snapshot and evidence submissions from now on.</param>
/// <param name="TeamName" example="Los Topos">Name as stored.</param>
/// <param name="ParticipantUserId" example="a3f1c7d2-8b4e-4f60-9a21-5c7e0d3b6f84">Authenticated identity now playing in the team.</param>
/// <param name="RegisteredAtUtc" example="2026-07-16T20:47:12Z">Instant the team was created.</param>
public sealed record RegisterTeamResponse(
    Guid LiveSessionId,
    Guid SessionTeamId,
    string TeamName,
    string ParticipantUserId,
    DateTimeOffset RegisteredAtUtc);

/// <summary>
/// Data a participant sends to play in a Session Team that already exists.
/// </summary>
/// <param name="JoinCode" example="H7KP2M">Session Join Code that selects the target LiveSession. Case-insensitive; six characters.</param>
/// <param name="SessionTeamId" example="6f2e5d84-1a7b-4c39-9e02-5b8d3f7a1c46">Team to join. It must belong to the session the code resolves to.</param>
public sealed record JoinSessionTeamRequest(string JoinCode, Guid SessionTeamId);

/// <summary>
/// Result of joining a Session Team: the participant's Team Participation for this execution.
/// </summary>
/// <param name="LiveSessionId" example="5e1d9f34-8b26-4a7c-9f10-3c2b5d6e7a80">Session the code resolved to.</param>
/// <param name="SessionTeamId" example="6f2e5d84-1a7b-4c39-9e02-5b8d3f7a1c46">Team joined. Addresses this team's snapshot and evidence submissions from now on.</param>
/// <param name="TeamName" example="Los Topos">Name of the joined team.</param>
/// <param name="ParticipantUserId" example="a3f1c7d2-8b4e-4f60-9a21-5c7e0d3b6f84">Authenticated identity now playing in the team.</param>
/// <param name="EnrolledAtUtc" example="2026-07-16T20:49:03Z">Instant the participant was attached to the team.</param>
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

