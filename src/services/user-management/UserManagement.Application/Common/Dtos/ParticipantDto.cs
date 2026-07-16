namespace UserManagement.Application.Common.Dtos;

/// <summary>
/// Public response returned after a participant self-registers. Only exposes the identifiers the
/// anonymous mobile client needs, deliberately omitting the synthesized names and email.
/// </summary>
/// <param name="UserId" example="7c9e6679-7425-40de-944b-e07fc1f90ae7">Keycloak user id assigned on registration; identifies the Participant when joining a session.</param>
/// <param name="Username" example="pao.rojas">The handle as stored in the realm, trimmed — may differ from the string that was submitted.</param>
public sealed record ParticipantDto(
    string UserId,
    string Username);
