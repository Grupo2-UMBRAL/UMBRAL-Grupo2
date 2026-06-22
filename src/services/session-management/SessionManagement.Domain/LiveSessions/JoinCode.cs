using Umbral.ServiceDefaults;

namespace SessionManagement.Domain.LiveSessions;

public sealed record JoinCode
{
    public const int Length = 6;
    public const string AllowedCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private JoinCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static JoinCode Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UmbralDomainException(
                "join_code_required",
                "Join Code is required.",
                UmbralFailureCategory.Validation);
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length != Length)
        {
            throw new UmbralDomainException(
                "join_code_length_invalid",
                $"Join Code must contain exactly {Length} characters.",
                UmbralFailureCategory.Validation);
        }

        if (normalized.Any(character => !AllowedCharacters.Contains(character, StringComparison.Ordinal)))
        {
            throw new UmbralDomainException(
                "join_code_characters_invalid",
                "Join Code contains unsupported characters.",
                UmbralFailureCategory.Validation);
        }

        return new JoinCode(normalized);
    }

    public override string ToString() => Value;
}
