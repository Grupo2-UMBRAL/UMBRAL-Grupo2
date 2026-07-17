using MediatR;
using Umbral.ServiceDefaults;
using UserManagement.Application.Abstractions;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Participants.Commands.ChangeParticipantUsername;

public sealed class ChangeParticipantUsernameCommandHandler(
    ICurrentParticipantIdentity currentParticipantIdentity,
    IParticipantAdministrationPort port)
    : IRequestHandler<ChangeParticipantUsernameCommand, ParticipantProfileDto>
{
    public async Task<ParticipantProfileDto> Handle(
        ChangeParticipantUsernameCommand request,
        CancellationToken cancellationToken)
    {
        var userId = currentParticipantIdentity.GetRequiredParticipantUserId();
        var normalizedUsername = request.Username!.Trim();

        // Following the precedent of UserCreationFlowHandler, with one difference the creation flow
        // cannot have: the holder may be the caller. Re-submitting your own handle is a no-op, not a
        // conflict. Keycloak's own 409 stays underneath as the backstop for the race between this
        // read and the write below.
        var currentHolderUserId = await port.FindUserIdByUsernameAsync(normalizedUsername, cancellationToken);

        if (currentHolderUserId is not null &&
            !string.Equals(currentHolderUserId, userId, StringComparison.Ordinal))
        {
            throw new UmbralDomainException(
                "participant_username_taken",
                "Username already belongs to another User.",
                UmbralFailureCategory.Conflict);
        }

        return await port.UpdateUsernameAsync(userId, normalizedUsername, cancellationToken);
    }
}
