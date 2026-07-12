using Umbral.ServiceDefaults;

namespace SessionManagement.Domain.LiveSessions;

// Flat per-item snapshot of a Play stored in the Session Flow JSON.
// (Domain vocabulary: this represents a "Play". The type name is kept as
// LiveSessionStage to avoid churn across persistence and call sites.)
// For Trivia plays it carries the selectable Choices plus the server-only
// CorrectChoiceId used to validate submissions; CorrectChoiceId must never be
// projected into a participant-facing response.
public sealed record LiveSessionStage
{
    public Guid MissionStageId { get; init; }

    public string Name { get; init; } = string.Empty;

    public int SessionStageOrder { get; init; }

    public int SourceOrder { get; init; }

    public int ResolvedTimeBudgetMinutes { get; init; }

    public string Difficulty { get; init; } = string.Empty;

    public string GameType { get; init; } = string.Empty;

    public string Prompt { get; init; } = string.Empty;

    public string? ExpectedQrHash { get; init; }

    public IReadOnlyList<LiveSessionChoice> Choices { get; init; } = Array.Empty<LiveSessionChoice>();

    public Guid? CorrectChoiceId { get; init; }

    public IReadOnlyList<LiveSessionStageHint> Hints { get; init; } = Array.Empty<LiveSessionStageHint>();

    public static LiveSessionStage Create(
        Guid missionStageId,
        string name,
        int sessionStageOrder,
        int sourceOrder,
        int resolvedTimeBudgetMinutes,
        string difficulty,
        string gameType,
        string prompt,
        string? expectedQrHash = null,
        IReadOnlyList<LiveSessionChoice>? choices = null,
        Guid? correctChoiceId = null,
        IReadOnlyList<LiveSessionStageHint>? hints = null)
    {
        if (missionStageId == Guid.Empty)
        {
            throw new UmbralDomainException(
                "live_session_stage_source_required",
                "LiveSession stage must reference a Mission Stage.",
                UmbralFailureCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new UmbralDomainException(
                "live_session_stage_name_required",
                "LiveSession stage name is required.",
                UmbralFailureCategory.Validation);
        }

        if (sessionStageOrder <= 0)
        {
            throw new UmbralDomainException(
                "live_session_stage_order_invalid",
                "LiveSession stage order must be greater than zero.",
                UmbralFailureCategory.Validation);
        }

        if (sourceOrder <= 0)
        {
            throw new UmbralDomainException(
                "live_session_stage_source_order_invalid",
                "LiveSession stage source order must be greater than zero.",
                UmbralFailureCategory.Validation);
        }

        if (resolvedTimeBudgetMinutes <= 0)
        {
            throw new UmbralDomainException(
                "live_session_stage_time_budget_invalid",
                "LiveSession stage time budget must be greater than zero.",
                UmbralFailureCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(difficulty))
        {
            throw new UmbralDomainException(
                "live_session_stage_difficulty_required",
                "LiveSession stage difficulty is required.",
                UmbralFailureCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(gameType))
        {
            throw new UmbralDomainException(
                "live_session_stage_game_type_required",
                "LiveSession stage game type is required.",
                UmbralFailureCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new UmbralDomainException(
                "live_session_stage_prompt_required",
                "LiveSession stage prompt is required.",
                UmbralFailureCategory.Validation);
        }

        var normalizedChoices = choices is null || choices.Count == 0
            ? Array.Empty<LiveSessionChoice>()
            : choices.ToArray();

        if (correctChoiceId.HasValue
            && normalizedChoices.Length > 0
            && Array.TrueForAll(normalizedChoices, choice => choice.Id != correctChoiceId.Value))
        {
            throw new UmbralDomainException(
                "live_session_stage_correct_choice_not_in_choices",
                "LiveSession stage correct choice must be one of its choices.",
                UmbralFailureCategory.Validation);
        }

        return new LiveSessionStage
        {
            MissionStageId = missionStageId,
            Name = name.Trim(),
            SessionStageOrder = sessionStageOrder,
            SourceOrder = sourceOrder,
            ResolvedTimeBudgetMinutes = resolvedTimeBudgetMinutes,
            Difficulty = difficulty.Trim(),
            GameType = gameType.Trim(),
            Prompt = prompt.Trim(),
            ExpectedQrHash = string.IsNullOrWhiteSpace(expectedQrHash) ? null : expectedQrHash.Trim(),
            Choices = normalizedChoices,
            CorrectChoiceId = correctChoiceId,
            Hints = hints?.ToArray() ?? Array.Empty<LiveSessionStageHint>()
        };
    }
}
