using UserManagement.Application.Abstractions;
using MediatR;
using Umbral.ServiceDefaults;
using UserManagement.Domain.Entities;

namespace UserManagement.Application.Features.Operators.Commands.RotateOperatorPassword;

public sealed class RotateOperatorPasswordCommandHandler(IOperatorAdministrationPort port)
    : IRequestHandler<RotateOperatorPasswordCommand, OperatorUser>
{
    public async Task<OperatorUser> Handle(RotateOperatorPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalizedUserId = request.UserId?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedUserId))
        {
            throw new UmbralDomainException(
                "operator_user_id_required",
                "Operator user id is required.",
                UmbralFailureCategory.Validation);
        }

        var operators = await port.ListOperatorsAsync(cancellationToken);
        var operatorUser = operators.FirstOrDefault(candidate => candidate.Id == normalizedUserId);

        if (operatorUser is null)
        {
            throw new UmbralDomainException(
                "operator_user_not_found",
                "Operator User was not found.",
                UmbralFailureCategory.NotFound);
        }

        var normalizedPassword = NormalizePassword(request.Password);
        
        // This is where we satisfy the rule: "The correct flow must pass first through the User Microservice endpoint... then communicate internally with Keycloak"
        await port.RotateOperatorPasswordAsync(normalizedUserId, normalizedPassword, cancellationToken);

        return operatorUser;
    }

    private static string NormalizePassword(string? value)
    {
        var password = value?.Trim();

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new UmbralDomainException(
                "operator_password_required",
                "Password is required.",
                UmbralFailureCategory.Validation);
        }

        if (password.Length > 128)
        {
            throw new UmbralDomainException(
                "operator_password_too_long",
                "Password must stay under 128 characters.",
                UmbralFailureCategory.Validation);
        }

        if (password.Length < 8)
        {
            throw new UmbralDomainException(
                "operator_password_too_short",
                "Password must contain at least 8 characters.",
                UmbralFailureCategory.Validation);
        }

        return password;
    }
}

