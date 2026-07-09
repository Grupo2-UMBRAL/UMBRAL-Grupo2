namespace UserManagement.Application.Common.Dtos;

/// <summary>
/// Public response returned after a participant self-registers. Only exposes the identifiers the
/// anonymous mobile client needs, deliberately omitting the synthesized names and email.
/// </summary>
public sealed record ParticipantDto(
    string UserId,
    string Username);
