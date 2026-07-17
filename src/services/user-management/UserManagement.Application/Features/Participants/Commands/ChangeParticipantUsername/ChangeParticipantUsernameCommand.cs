using MediatR;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Participants.Commands.ChangeParticipantUsername;

/// <summary>
/// Renames the calling Participant. Carries no user id: the account is the one in the token.
/// </summary>
/// <param name="Username" example="pao.rojas">The new handle, held to the same rules as registration. Must be free in the realm (409 otherwise). Re-submitting the Participant's current handle is accepted as a no-op. This is also the handle they log in with, so the mobile screen warns before applying it.</param>
public sealed record ChangeParticipantUsernameCommand(
    string? Username) : IRequest<ParticipantProfileDto>;
