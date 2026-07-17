using MediatR;

namespace UserManagement.Application.Features.Participants.Commands.DeactivateParticipantAccount;

/// <summary>
/// Deactivates the calling Participant's own account. Carries no user id: the account is the one in
/// the token. Returns nothing -- there is no post-state worth handing back to a client that is about
/// to be signed out, so the endpoint answers 204.
/// </summary>
public sealed record DeactivateParticipantAccountCommand : IRequest;
