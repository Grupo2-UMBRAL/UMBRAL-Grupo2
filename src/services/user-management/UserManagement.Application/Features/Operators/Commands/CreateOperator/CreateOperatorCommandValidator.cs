using Umbral.ServiceDefaults;
using UserManagement.Application.Common;

namespace UserManagement.Application.Features.Operators.Commands.CreateOperator;

public static class CreateOperatorCommandValidator
{
    public static void Validate(CreateOperatorCommand request)
    {
        NormalizeUsername(request.Username);
        NormalizeEmail(request.Email);
        NormalizeRequiredText(request.FirstName, "First name", 80, "operator_first_name");
        NormalizeRequiredText(request.LastName, "Last name", 80, "operator_last_name");
        PasswordPolicy.Normalize(request.Password, "operator_password");
    }

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
}

