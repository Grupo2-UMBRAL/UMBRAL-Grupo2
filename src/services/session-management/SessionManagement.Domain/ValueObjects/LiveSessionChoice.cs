using Umbral.ServiceDefaults;

namespace SessionManagement.Domain.LiveSessions;

// Selectable Trivia answer option copied into the Session Flow for a Play.
// Only Id and Text are stored; whether it is the correct option lives in
// LiveSessionStage.CorrectChoiceId and is never projected to participants.
public sealed record LiveSessionChoice
{
    public Guid Id { get; init; }

    public string Text { get; init; } = string.Empty;

    public static LiveSessionChoice Create(Guid id, string text)
    {
        if (id == Guid.Empty)
        {
            throw new UmbralDomainException(
                "live_session_choice_id_required",
                "LiveSession choice must have an identifier.",
                UmbralFailureCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new UmbralDomainException(
                "live_session_choice_text_required",
                "LiveSession choice text is required.",
                UmbralFailureCategory.Validation);
        }

        return new LiveSessionChoice
        {
            Id = id,
            Text = text.Trim()
        };
    }
}
