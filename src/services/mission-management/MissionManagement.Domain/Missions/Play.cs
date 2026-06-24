using Umbral.ServiceDefaults;

namespace MissionManagement.Domain.Missions;

/// <summary>
/// The scored atom of a <see cref="Challenge"/>. Concrete subtypes are <see cref="Question"/>
/// (Trivia) and <see cref="Search"/> (Treasure Hunt). A Play may override the Challenge default
/// Difficulty / Time Limit; the effective value is <c>override ?? challenge default</c>.
/// </summary>
public abstract class Play
{
    private protected Play()
    {
    }

    private protected Play(Guid id, Guid challengeId, int order, string? difficultyOverride, int? timeLimitMinutesOverride)
    {
        Id = id;
        ChallengeId = challengeId;
        Order = order;
        DifficultyOverride = difficultyOverride;
        TimeLimitMinutesOverride = timeLimitMinutesOverride;
    }

    public Guid Id { get; private protected set; }

    public Guid ChallengeId { get; private protected set; }

    public int Order { get; private protected set; }

    public string? DifficultyOverride { get; private protected set; }

    public int? TimeLimitMinutesOverride { get; private protected set; }

    /// <summary>The Game Type this play belongs to. Used to enforce homogeneity with the owning Challenge.</summary>
    public abstract string GameType { get; }

    /// <summary>The player-facing prompt (Question text or Search clue).</summary>
    public abstract string Prompt { get; }

    public string ResolveDifficulty(string challengeDefaultDifficulty)
    {
        return Difficulty.Normalize(DifficultyOverride ?? challengeDefaultDifficulty);
    }

    public int ResolveTimeLimitMinutes(int challengeDefaultTimeLimitMinutes)
    {
        return TimeLimitMinutesOverride ?? challengeDefaultTimeLimitMinutes;
    }

    private protected static string? NormalizeDifficultyOverride(string? difficultyOverride)
    {
        return string.IsNullOrWhiteSpace(difficultyOverride)
            ? null
            : Difficulty.Normalize(difficultyOverride);
    }

    private protected static int? NormalizeTimeLimitOverride(int? timeLimitMinutesOverride)
    {
        if (timeLimitMinutesOverride is null)
        {
            return null;
        }

        if (timeLimitMinutesOverride <= 0)
        {
            throw new UmbralDomainException(
                "play_time_limit_override_invalid",
                "Play time limit override must be greater than zero when provided.",
                UmbralFailureCategory.Validation);
        }

        return timeLimitMinutesOverride;
    }

    private protected static int NormalizeOrder(int order)
    {
        return DomainText.NormalizeOrder(order, "play_order_invalid", "Play order must be greater than zero.");
    }
}
