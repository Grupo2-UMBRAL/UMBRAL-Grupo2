using UserManagement.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;
using Umbral.ServiceDefaults;
using UserManagement.Application.Common;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Operators.Commands.RotateOperatorPassword;

public sealed class RotateOperatorPasswordCommandHandler(
    IOperatorAdministrationPort port,
    IEmailNotificationService emailService,
    ILogger<RotateOperatorPasswordCommandHandler> logger)
    : IRequestHandler<RotateOperatorPasswordCommand, OperatorDto>
{
    public async Task<OperatorDto> Handle(RotateOperatorPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalizedUserId = request.UserId.Trim();
        var normalizedPassword = PasswordPolicy.Normalize(request.Password, "operator_password");

        var operators = await port.ListOperatorsAsync(cancellationToken);
        var operatorUser = operators.FirstOrDefault(candidate => candidate.Id == normalizedUserId);

        if (operatorUser is null)
        {
            throw new UmbralDomainException(
                "operator_user_not_found",
                "Operator User was not found.",
                UmbralFailureCategory.NotFound);
        }

        // The correct flow passes first through the User microservice endpoint, which then talks to
        // Keycloak internally through the infrastructure port.
        await port.RotateOperatorPasswordAsync(normalizedUserId, normalizedPassword, cancellationToken);

        // Notification — fire-and-forget semantics; failure does NOT revert the password change.
        try
        {
            await emailService.SendPasswordRotatedAsync(
                operatorUser.Email, operatorUser.Username, normalizedPassword, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to send password-rotated email to {Email}. The password was changed successfully.",
                operatorUser.Email);
        }

        return operatorUser;
    }
}
