using Umbral.ServiceDefaults;

namespace MissionManagement.Domain.Missions;

public sealed class Choice
{
    private Choice()
    {
    }

    private Choice(Guid id, Guid questionId, int order, string text, bool isCorrect)
    {
        Id = id;
        QuestionId = questionId;
        Order = order;
        Text = text;
        IsCorrect = isCorrect;
    }

    public Guid Id { get; private set; }

    public Guid QuestionId { get; private set; }

    public int Order { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public bool IsCorrect { get; private set; }

    public static Choice Create(Guid id, Guid questionId, int order, string text, bool isCorrect)
    {
        var normalizedOrder = DomainText.NormalizeOrder(
            order,
            "choice_order_invalid",
            "Choice order must be greater than zero.");
        var normalizedText = DomainText.NormalizeRequired(
            text,
            "choice_text_required",
            "Choice text is required.",
            512);

        return new Choice(id, questionId, normalizedOrder, normalizedText, isCorrect);
    }
}
