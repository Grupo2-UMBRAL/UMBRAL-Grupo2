using Umbral.ServiceDefaults;

namespace Umbral.IdentityAccess.Api.Application.Operators;

public sealed class OperatorAdministrationService(IOperatorAdministrationPort port)
{
    public async Task<IReadOnlyList<OperatorUser>> ListOperatorsAsync(CancellationToken cancellationToken)
    {
        var operators = await port.ListOperatorsAsync(cancellationToken);

        return operators
            .OrderByDescending(static user => user.IsActive)
            .ThenBy(static user => user.Username, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<OperatorUser> CreateOperatorAsync(
        CreateOperatorInput input,
        CancellationToken cancellationToken)
    {
        var validatedInput = ValidateCreateOperatorInput(input);
        var usersWithSameEmail = await port.FindUsersByEmailAsync(validatedInput.Email, cancellationToken);

        if (usersWithSameEmail.Count > 0)
        {
            throw new UmbralDomainException(
                "operator_email_duplicate",
                "Email already belongs to another User.",
                UmbralFailureCategory.Conflict);
        }

        var usersWithSameUsername = await port.FindUsersByUsernameAsync(validatedInput.Username, cancellationToken);

        if (usersWithSameUsername.Count > 0)
        {
            throw new UmbralDomainException(
                "operator_username_duplicate",
                "Username already belongs to another User.",
                UmbralFailureCategory.Conflict);
        }

        var createdUser = await port.CreateUserAsync(validatedInput, cancellationToken);

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
            catch
            {
            }

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

    public async Task<OperatorUser> DeactivateOperatorAsync(string userId, CancellationToken cancellationToken)
    {
        var normalizedUserId = userId.Trim();

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

        if (!operatorUser.IsActive)
        {
            return operatorUser;
        }

        return await port.SetUserEnabledAsync(normalizedUserId, enabled: false, cancellationToken);
    }

    public async Task<OperatorUser> RotateOperatorPasswordAsync(
        string userId,
        string? password,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = userId.Trim();

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

        var normalizedPassword = NormalizePassword(password);
        await port.RotateOperatorPasswordAsync(normalizedUserId, normalizedPassword, cancellationToken);

        return operatorUser;
    }

    public static ValidatedCreateOperatorInput ValidateCreateOperatorInput(CreateOperatorInput input) =>
        new(
            NormalizeUsername(input.Username),
            NormalizeEmail(input.Email),
            NormalizeRequiredText(input.FirstName, "First name", 80, "operator_first_name"),
            NormalizeRequiredText(input.LastName, "Last name", 80, "operator_last_name"),
            NormalizePassword(input.Password));

    private static string NormalizeRequiredText(string? value, string fieldName, int maximumLength, string code)
    {
        var normalized = value?.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new UmbralDomainException(
                $"{code}_required",
                $"{fieldName} is required.",
                UmbralFailureCategory.Validation);
        }

        if (normalized.Length > maximumLength)
        {
            throw new UmbralDomainException(
                $"{code}_too_long",
                $"{fieldName} must stay under {maximumLength} characters.",
                UmbralFailureCategory.Validation);
        }

        return normalized;
    }

    private static string NormalizeUsername(string? value)
    {
        var username = NormalizeRequiredText(value, "Username", 40, "operator_username");

        if (!System.Text.RegularExpressions.Regex.IsMatch(username, "^[A-Za-z0-9._-]+$"))
        {
            throw new UmbralDomainException(
                "operator_username_invalid",
                "Username only admits letters, digits, dot, underscore, or dash.",
                UmbralFailureCategory.Validation);
        }

        return username;
    }

    private static string NormalizeEmail(string? value)
    {
        var email = NormalizeRequiredText(value, "Email", 120, "operator_email").ToLowerInvariant();

        if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
        {
            throw new UmbralDomainException(
                "operator_email_invalid",
                "Email format is invalid.",
                UmbralFailureCategory.Validation);
        }

        return email;
    }

    private static string NormalizePassword(string? value)
    {
        var password = NormalizeRequiredText(value, "Password", 128, "operator_password");

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
