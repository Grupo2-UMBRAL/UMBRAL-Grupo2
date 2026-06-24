using Umbral.ServiceDefaults;

namespace MissionManagement.Domain.Missions;

/// <summary>
/// The playable leaf of the Composite pattern. A Challenge carries the shared configuration once
/// (Game Type, default Difficulty, default Time Limit) and owns an ordered list of homogeneous
/// <see cref="Play"/>s: a Trivia Challenge holds only <see cref="Question"/>s, a Treasure Hunt
/// Challenge holds only <see cref="Search"/>es. A Challenge never nests.
/// </summary>
public sealed class Challenge : PathItem
{
    private readonly List<Play> _plays = new();

    private Challenge()
    {
    }

    private Challenge(
        Guid id,
        Guid missionId,
        Guid? parentSectionId,
        int order,
        string title,
        string gameType,
        string defaultDifficulty,
        int defaultTimeLimitMinutes,
        bool isActive)
        : base(id, missionId, parentSectionId, order)
    {
        Title = title;
        GameType = gameType;
        DefaultDifficulty = defaultDifficulty;
        DefaultTimeLimitMinutes = defaultTimeLimitMinutes;
        IsActive = isActive;
    }

    public string Title { get; private set; } = string.Empty;

    public string GameType { get; private set; } = string.Empty;

    public string DefaultDifficulty { get; private set; } = string.Empty;

    public int DefaultTimeLimitMinutes { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyList<Play> Plays => _plays;

    public static Challenge Create(
        Guid id,
        Guid missionId,
        Guid? parentSectionId,
        int order,
        string title,
        string gameType,
        string defaultDifficulty,
        int defaultTimeLimitMinutes,
        bool isActive,
        IReadOnlyList<Play>? plays = null)
    {
        var normalizedTimeLimit = NormalizeTimeLimit(defaultTimeLimitMinutes);

        var challenge = new Challenge(
            id,
            missionId,
            parentSectionId,
            NormalizeOrder(order),
            DomainText.NormalizeRequired(title, "challenge_title_required", "Challenge title is required.", 120),
            MissionGameType.Normalize(gameType),
            Difficulty.Normalize(defaultDifficulty),
            normalizedTimeLimit,
            isActive);

        challenge.SetPlays(plays ?? Array.Empty<Play>());

        return challenge;
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            throw new UmbralDomainException(
                "challenge_already_inactive",
                "Challenge is already inactive.",
                UmbralFailureCategory.Conflict);
        }

        IsActive = false;
    }

    /// <summary>True when the Challenge has at least one play and every play matches its Game Type.</summary>
    public bool IsWellFormed => _plays.Count > 0 && _plays.All(MatchesGameType);

    /// <summary>Attaches an already-constructed play during in-memory rehydration. Skips re-validation.</summary>
    internal void AttachPlay(Play play)
    {
        _plays.Add(play);
    }

    internal void SortPlays()
    {
        _plays.Sort(static (left, right) => left.Order.CompareTo(right.Order));
    }

    private bool MatchesGameType(Play play)
    {
        return string.Equals(play.GameType, GameType, StringComparison.OrdinalIgnoreCase);
    }

    private void SetPlays(IReadOnlyList<Play> plays)
    {
        var ordered = plays.OrderBy(play => play.Order).ToList();

        var duplicateOrder = ordered
            .GroupBy(play => play.Order)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateOrder is not null)
        {
            throw new UmbralDomainException(
                "play_order_duplicate",
                $"Play order '{duplicateOrder.Key}' is duplicated within the challenge.",
                UmbralFailureCategory.Validation);
        }

        foreach (var play in ordered)
        {
            if (!MatchesGameType(play))
            {
                throw new UmbralDomainException(
                    "challenge_play_game_type_mismatch",
                    $"Challenge Game Type '{GameType}' does not accept a '{play.GameType}' play.",
                    UmbralFailureCategory.Validation);
            }
        }

        _plays.Clear();
        _plays.AddRange(ordered);
    }

    private static int NormalizeTimeLimit(int defaultTimeLimitMinutes)
    {
        if (defaultTimeLimitMinutes <= 0)
        {
            throw new UmbralDomainException(
                "challenge_time_limit_invalid",
                "Challenge default time limit must be greater than zero.",
                UmbralFailureCategory.Validation);
        }

        return defaultTimeLimitMinutes;
    }
}
