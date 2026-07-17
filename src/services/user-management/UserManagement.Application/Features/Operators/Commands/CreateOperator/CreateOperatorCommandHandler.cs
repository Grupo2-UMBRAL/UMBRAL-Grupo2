using UserManagement.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Common.Handlers;

namespace UserManagement.Application.Features.Operators.Commands.CreateOperator;

public sealed class CreateOperatorCommandHandler(
    UserCreationFlowHandler flowHandler,
    IOperatorAdministrationPort port,
    ILogger<CreateOperatorCommandHandler> logger)
    : IRequestHandler<CreateOperatorCommand, OperatorDto>
{
    public async Task<OperatorDto> Handle(CreateOperatorCommand request, CancellationToken cancellationToken)
    {
        // No password and no names: the account is created credential-less. The Operator sets their own
        // password and fills in their profile through the onboarding link Keycloak emails next.
        var operatorUser = await flowHandler.CreateUserAsync(
            request.Email!,
            request.Username!,
            firstName: null,
            lastName: null,
            password: null,
            isOperator: true,
            cancellationToken);

        // Onboarding invitation — awaited, but its failure does NOT revert the operator: a timeout is
        // ambiguous (Keycloak may already have sent the mail), and the account must survive so the
        // Administrator can resend the invitation instead of creating a duplicate.
        try
        {
            await port.SendOperatorOnboardingInvitationAsync(operatorUser.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to send the onboarding invitation to {Email}. The operator {UserId} was created; " +
                "an Administrator can resend the invitation.",
                operatorUser.Email, operatorUser.Id);
        }

        return operatorUser;
    }
}
