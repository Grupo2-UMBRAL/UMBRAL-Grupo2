using Umbral.ServiceDefaults;

namespace SessionManagement.Domain.LiveSessions;

public sealed record ParticipantUserId
{
    public const int MaximumLength = 120;

    private ParticipantUserId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ParticipantUserId Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UmbralDomainException(
                "participant_user_id_required",
                "Authenticated participant id is required.",
                UmbralFailureCategory.Validation);
        }

        var normalized = value.Trim();
        if (normalized.Length > MaximumLength)
        {
            throw new UmbralDomainException(
                "participant_user_id_too_long",
                $"Authenticated participant id cannot exceed {MaximumLength} characters.",
                UmbralFailureCategory.Validation);
        }

        return new ParticipantUserId(normalized);
    }

    public override string ToString() => Value;
}
