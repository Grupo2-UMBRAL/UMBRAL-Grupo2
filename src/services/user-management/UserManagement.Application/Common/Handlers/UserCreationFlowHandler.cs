using Microsoft.Extensions.Logging;
using Umbral.ServiceDefaults;
using UserManagement.Application.Abstractions;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Common.Handlers;

/// <summary>
/// Encapsulates the duplicated creation, uniqueness check, and rollback logic
/// for creating Keycloak users (both Operators and Participants).
/// </summary>
public sealed class UserCreationFlowHandler(
    IOperatorAdministrationPort port,
    ILogger<UserCreationFlowHandler> logger)
{
    public async Task<OperatorDto> CreateUserAsync(
        string email,
        string username,
        string? firstName,
        string? lastName,
        string? password,
        bool isOperator,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedUsername = username.Trim();
        var userType = isOperator ? "operator" : "participant";

        // 1. Uniqueness Checks
        var usersWithSameEmail = await port.FindUsersByEmailAsync(normalizedEmail, cancellationToken);
        if (usersWithSameEmail.Count > 0)
        {
            throw new UmbralDomainException(
                $"{userType}_email_duplicate",
                "Email already belongs to another User.",
                UmbralFailureCategory.Conflict);
        }

        var usersWithSameUsername = await port.FindUsersByUsernameAsync(normalizedUsername, cancellationToken);
        if (usersWithSameUsername.Count > 0)
        {
            throw new UmbralDomainException(
                $"{userType}_username_duplicate",
                "Username already belongs to another User.",
                UmbralFailureCategory.Conflict);
        }

        // 2. Execution — names and password are optional (Operators complete them via onboarding),
        // so trim only what is present and let the port decide what a null password means.
        var createdUserId = await port.CreateUserAsync(
            normalizedUsername,
            normalizedEmail,
            firstName?.Trim(),
            lastName?.Trim(),
            password?.Trim(),
            cancellationToken);

        // 3. Role Assignment & Rollback
        try
        {
            if (isOperator)
            {
                await port.AssignOperatorRoleAsync(createdUserId, cancellationToken);
            }
            else
            {
                await port.AssignParticipantRoleAsync(createdUserId, cancellationToken);
            }
        }
        catch
        {
            try
            {
                await port.DeleteUserAsync(createdUserId, cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                logger.LogError(
                    rollbackEx,
                    "Failed to roll back Keycloak user {UserId} ({UserType}) after role assignment failed; the user may be orphaned.",
                    createdUserId,
                    userType);
            }
            throw;
        }

        // 4. Recovery
        var createdUser = await port.GetUserByIdAsync(createdUserId, cancellationToken);

        if (createdUser is null)
        {
            throw new UmbralTechnicalException(
                $"{userType}_user_recovery_failed",
                "Keycloak created the User but did not return it afterwards.");
        }

        return createdUser;
    }
}
