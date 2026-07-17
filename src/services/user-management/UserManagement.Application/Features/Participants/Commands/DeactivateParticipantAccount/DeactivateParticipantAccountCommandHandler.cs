using MediatR;
using Microsoft.Extensions.Logging;
using UserManagement.Application.Abstractions;

namespace UserManagement.Application.Features.Participants.Commands.DeactivateParticipantAccount;

public sealed class DeactivateParticipantAccountCommandHandler(
    ICurrentParticipantIdentity currentParticipantIdentity,
    IParticipantAdministrationPort port,
    ILogger<DeactivateParticipantAccountCommandHandler> logger)
    : IRequestHandler<DeactivateParticipantAccountCommand>
{
    public async Task Handle(
        DeactivateParticipantAccountCommand request,
        CancellationToken cancellationToken)
    {
        var userId = currentParticipantIdentity.GetRequiredParticipantUserId();

        // Two remote effects, no transaction, so the semantics are explicit rather than incidental.
        // The disable must succeed: if it throws, nothing happened and a retry is safe.
        await port.DeactivateParticipantAsync(userId, cancellationToken);

        try
        {
            // The logout is best effort. By this line the account is already disabled: it cannot log
            // in, and the refresh grant fails against a disabled user, so the only residue is an
            // access token already in the player's hands -- bounded by accessTokenLifespan, and
            // discarded anyway when the app signs out.
            //
            // Reporting a failure here would tell someone whose account IS deactivated that it was
            // not. That is worse than the window, so the partial state goes to the log, not to the
            // caller. No invented 202 or 207.
            await port.LogoutParticipantSessionsAsync(userId, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Deactivated participant {UserId} but could not revoke their Keycloak sessions; any access token already issued stays valid until it expires.",
                userId);
        }
    }
}
