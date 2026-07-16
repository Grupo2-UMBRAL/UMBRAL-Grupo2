using MediatR;

using System.Text.Json.Serialization;

namespace SessionManagement.Application.Features.SessionEnrollment;

/// <summary>
/// Data an operator sends to create a Session Team on participants' behalf, for teams formed
/// off-app. Unlike participant registration, it enrols nobody: the team starts with no members.
/// </summary>
/// <param name="TeamName" example="Los Topos">Name for the new team, shown to participants choosing a group.</param>
public sealed record RegisterTeamByOperatorRequest([property: JsonPropertyName("teamName")] string TeamName);

public sealed record RegisterTeamByOperatorCommand(Guid LiveSessionId, string TeamName)
    : IRequest<RegisterTeamByOperatorResponse>;

/// <summary>
/// The Session Team an operator just created inside a LiveSession.
/// </summary>
/// <param name="LiveSessionId" example="5e1d9f34-8b26-4a7c-9f10-3c2b5d6e7a80">Session the team was created in.</param>
/// <param name="SessionTeamId" example="6f2e5d84-1a7b-4c39-9e02-5b8d3f7a1c46">The new team. Participants can now pick it by this id when joining.</param>
/// <param name="TeamName" example="Los Topos">Name as stored.</param>
/// <param name="CreatedAtUtc" example="2026-07-16T20:50:00Z">Instant the team was created.</param>
public sealed record RegisterTeamByOperatorResponse(
    Guid LiveSessionId,
    Guid SessionTeamId,
    string TeamName,
    DateTimeOffset CreatedAtUtc);
