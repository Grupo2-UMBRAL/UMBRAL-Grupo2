namespace MissionManagement.Domain.Missions;

/// <summary>
/// A single play resolved against its owning Challenge, positioned by a global 1-based
/// <see cref="Order"/> across the depth-first flatten of a <see cref="Mission"/>. This is the
/// cross-context unit the session/scoring slices call a "stage".
/// </summary>
public sealed record FlattenedPlay
{
    public required Guid Id { get; init; }

    /// <summary>Global 1-based position across the whole flattened sequence.</summary>
    public required int Order { get; init; }

    public required string GameType { get; init; }

    public required string Difficulty { get; init; }

    public required int TimeLimitMinutes { get; init; }

    public required string Prompt { get; init; }

    /// <summary>Present for Trivia plays; empty for Treasure Hunt.</summary>
    public IReadOnlyList<FlattenedChoice> Choices { get; init; } = Array.Empty<FlattenedChoice>();

    /// <summary>The id of the correct choice for Trivia plays; null for Treasure Hunt. Server-to-server only.</summary>
    public Guid? CorrectChoiceId { get; init; }

    /// <summary>The expected QR hash for Treasure Hunt plays; null for Trivia.</summary>
    public string? ExpectedQrHash { get; init; }

    /// <summary>Hints for Treasure Hunt plays; empty for Trivia.</summary>
    public IReadOnlyList<FlattenedHint> Hints { get; init; } = Array.Empty<FlattenedHint>();
}

public sealed record FlattenedChoice(Guid Id, string Text);

public sealed record FlattenedHint(string Content, bool IsSolution, double? Latitude, double? Longitude);
