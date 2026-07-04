using Umbral.ServiceDefaults;

namespace UserManagement.Application.Features.Participants.Commands.CreateParticipant;

public static class CreateParticipantCommandValidator
{
    public static void Validate(CreateParticipantCommand request)
    {
        NormalizeUsername(request.Username);
        NormalizeEmail(request.Email);
        NormalizePassword(request.Password);
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
        var username = NormalizeRequiredText(value, "Username", 40, "participant_username");

        if (username.Length < 3)
        {
            throw new UmbralDomainException(
                "participant_username_too_short",
                "Username must contain at least 3 characters.",
                UmbralFailureCategory.Validation);
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(username, "^[A-Za-z0-9._-]+$"))
        {
            throw new UmbralDomainException(
                "participant_username_invalid",
                "Username only admits letters, digits, dot, underscore, or dash.",
                UmbralFailureCategory.Validation);
        }

        return username;
    }

    private static string NormalizeEmail(string? value)
    {
        var email = NormalizeRequiredText(value, "Email", 120, "participant_email").ToLowerInvariant();

        if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
        {
            throw new UmbralDomainException(
                "participant_email_invalid",
                "Email format is invalid.",
                UmbralFailureCategory.Validation);
        }

        return email;
    }

    private static string NormalizePassword(string? value)
    {
        var password = NormalizeRequiredText(value, "Password", 128, "participant_password");

        if (password.Length < 8)
        {
            throw new UmbralDomainException(
                "participant_password_too_short",
                "Password must contain at least 8 characters.",
                UmbralFailureCategory.Validation);
        }

        return password;
    }
}
