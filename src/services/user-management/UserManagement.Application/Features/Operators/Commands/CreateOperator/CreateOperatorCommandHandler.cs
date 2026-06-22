using UserManagement.Application.Abstractions;
using MediatR;
using Umbral.ServiceDefaults;
using UserManagement.Domain.Entities;

namespace UserManagement.Application.Features.Operators.Commands.CreateOperator;

public sealed class CreateOperatorCommandHandler(IOperatorAdministrationPort port)
    : IRequestHandler<CreateOperatorCommand, OperatorUser>
{
    public async Task<OperatorUser> Handle(CreateOperatorCommand request, CancellationToken cancellationToken)
    {
        // 1. Validation
        CreateOperatorCommandValidator.Validate(request);
        
        var normalizedEmail = request.Email!.Trim().ToLowerInvariant();
        var normalizedUsername = request.Username!.Trim();

        // 2. Business Rules / Uniqueness Checks
        var usersWithSameEmail = await port.FindUsersByEmailAsync(normalizedEmail, cancellationToken);
        if (usersWithSameEmail.Count > 0)
        {
            throw new UmbralDomainException(
                "operator_email_duplicate",
                "Email already belongs to another User.",
                UmbralFailureCategory.Conflict);
        }

        var usersWithSameUsername = await port.FindUsersByUsernameAsync(normalizedUsername, cancellationToken);
        if (usersWithSameUsername.Count > 0)
        {
            throw new UmbralDomainException(
                "operator_username_duplicate",
                "Username already belongs to another User.",
                UmbralFailureCategory.Conflict);
        }

        // 3. Execution (Keycloak Creation via Infrastructure port)
        var createdUser = await port.CreateUserAsync(
            normalizedUsername,
            normalizedEmail,
            request.FirstName!.Trim(),
            request.LastName!.Trim(),
            request.Password!.Trim(),
            cancellationToken);

        try
        {
            await port.AssignOperatorRoleAsync(createdUser.Id, cancellationToken);
        }
        catch
        {
            try
            {
                await port.DeleteUserAsync(createdUser.Id, cancellationToken);
            }
            catch { }
            throw;
        }

        var operatorUser = await port.GetUserByIdAsync(createdUser.Id, cancellationToken);

        if (operatorUser is null)
        {
            throw new UmbralTechnicalException(
                "operator_user_recovery_failed",
                "Keycloak created the User but did not return it afterwards.");
        }

        return operatorUser;
    }
}

