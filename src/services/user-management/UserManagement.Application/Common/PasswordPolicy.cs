using Umbral.ServiceDefaults;

namespace UserManagement.Application.Common;

/// <summary>
/// Single source of truth for the operator/participant password rules (required, 8..128 chars).
/// Consolidates the check that previously lived duplicated across create-operator,
/// create-participant, and rotate-password. The <paramref name="codePrefix"/> preserves the
/// per-feature error codes (e.g. <c>operator_password_*</c>, <c>participant_password_*</c>) so the
/// HTTP contract is unchanged.
/// </summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 8;
    public const int MaximumLength = 128;

    /// <summary>Validates and returns the trimmed password, throwing a categorized domain exception on failure.</summary>
    public static string Normalize(string? value, string codePrefix)
    {
        var password = value?.Trim();

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new UmbralDomainException(
                $"{codePrefix}_required",
                "Password is required.",
                UmbralFailureCategory.Validation);
        }

        if (password.Length > MaximumLength)
        {
            throw new UmbralDomainException(
                $"{codePrefix}_too_long",
                $"Password must stay under {MaximumLength} characters.",
                UmbralFailureCategory.Validation);
        }

        if (password.Length < MinimumLength)
        {
            throw new UmbralDomainException(
                $"{codePrefix}_too_short",
                $"Password must contain at least {MinimumLength} characters.",
                UmbralFailureCategory.Validation);
        }

        return password;
    }
}
