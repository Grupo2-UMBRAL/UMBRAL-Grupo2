using UserManagement.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;
using Umbral.ServiceDefaults;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Common.Handlers;

namespace UserManagement.Application.Features.Operators.Commands.CreateOperator;

public sealed class CreateOperatorCommandHandler(
    UserCreationFlowHandler flowHandler,
    IEmailNotificationService emailService,
    ILogger<CreateOperatorCommandHandler> logger)
    : IRequestHandler<CreateOperatorCommand, OperatorDto>
{
    public async Task<OperatorDto> Handle(CreateOperatorCommand request, CancellationToken cancellationToken)
    {
        var operatorUser = await flowHandler.CreateUserAsync(
            request.Email!,
            request.Username!,
            request.FirstName!,
            request.LastName!,
            request.Password!,
            isOperator: true,
            cancellationToken);

        // 4. Notification — fire-and-forget semantics; failure does NOT revert the operator.
        try
        {
            await emailService.SendOperatorCredentialsAsync(
                operatorUser.Email, operatorUser.Username, request.Password!.Trim(), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to send credentials email to {Email}. The operator was created successfully.",
                operatorUser.Email);
        }

        return operatorUser;
    }
}

