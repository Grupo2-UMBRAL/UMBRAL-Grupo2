using Umbral.ServiceDefaults;

namespace MissionManagement.Domain.Missions;

/// <summary>A Trivia play: a text prompt with 2..4 ordered Choices, exactly one of which is correct.</summary>
public sealed class Question : Play
{
    private readonly List<Choice> _choices = new();

    private Question()
    {
    }

    private Question(
        Guid id,
        Guid challengeId,
        int order,
        string text,
        string? difficultyOverride,
        int? timeLimitMinutesOverride)
        : base(id, challengeId, order, difficultyOverride, timeLimitMinutesOverride)
    {
        Text = text;
    }

    public string Text { get; private set; } = string.Empty;

    public IReadOnlyList<Choice> Choices => _choices;

    public override string GameType => MissionGameType.Trivia;

    public override string Prompt => Text;

    /// <summary>Attaches an already-constructed choice during in-memory rehydration. Skips re-validation.</summary>
    internal void AttachChoice(Choice choice)
    {
        _choices.Add(choice);
    }

    internal void SortChoices()
    {
        _choices.Sort(static (left, right) => left.Order.CompareTo(right.Order));
    }

    public static Question Create(
        Guid id,
        Guid challengeId,
        int order,
        string text,
        string? difficultyOverride,
        int? timeLimitMinutesOverride,
        IReadOnlyList<Choice> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);

        var question = new Question(
            id,
            challengeId,
            NormalizeOrder(order),
            DomainText.NormalizeRequired(text, "question_text_required", "Question text is required.", 1_024),
            NormalizeDifficultyOverride(difficultyOverride),
            NormalizeTimeLimitOverride(timeLimitMinutesOverride));

        question.SetChoices(choices);

        return question;
    }

    private void SetChoices(IReadOnlyList<Choice> choices)
    {
        var ordered = choices.OrderBy(choice => choice.Order).ToList();

        if (ordered.Count < 2 || ordered.Count > 4)
        {
            throw new UmbralDomainException(
                "question_choice_count_invalid",
                "A Question must define between 2 and 4 choices.",
                UmbralFailureCategory.Validation);
        }

        var duplicateOrder = ordered
            .GroupBy(choice => choice.Order)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateOrder is not null)
        {
            throw new UmbralDomainException(
                "question_choice_order_duplicate",
                $"Choice order '{duplicateOrder.Key}' is duplicated within the question.",
                UmbralFailureCategory.Validation);
        }

        var correctCount = ordered.Count(choice => choice.IsCorrect);
        if (correctCount != 1)
        {
            throw new UmbralDomainException(
                "question_correct_choice_required",
                "A Question must have exactly one correct choice.",
                UmbralFailureCategory.Validation);
        }

        _choices.Clear();
        _choices.AddRange(ordered);
    }
}
