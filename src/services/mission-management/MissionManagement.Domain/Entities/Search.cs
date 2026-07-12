using Umbral.ServiceDefaults;

namespace MissionManagement.Domain.Missions;

/// <summary>A Treasure Hunt play: a clue prompt leading to an expected QR hash, with optional ordered Hints.</summary>
public sealed class Search : Play
{
    private readonly List<Hint> _hints = new();

    private Search()
    {
    }

    private Search(
        Guid id,
        Guid challengeId,
        int order,
        string clue,
        string expectedQrHash,
        string? difficultyOverride,
        int? timeLimitMinutesOverride)
        : base(id, challengeId, order, difficultyOverride, timeLimitMinutesOverride)
    {
        Clue = clue;
        ExpectedQrHash = expectedQrHash;
    }

    public string Clue { get; private set; } = string.Empty;

    public string ExpectedQrHash { get; private set; } = string.Empty;

    public IReadOnlyList<Hint> Hints => _hints;

    public override string GameType => MissionGameType.TreasureHunt;

    public override string Prompt => Clue;

    /// <summary>Attaches an already-constructed hint during in-memory rehydration. Skips re-validation.</summary>
    internal void AttachHint(Hint hint)
    {
        _hints.Add(hint);
    }

    internal void SortHints()
    {
        _hints.Sort(static (left, right) => left.Order.CompareTo(right.Order));
    }

    public static Search Create(
        Guid id,
        Guid challengeId,
        int order,
        string clue,
        string expectedQrHash,
        string? difficultyOverride,
        int? timeLimitMinutesOverride,
        IReadOnlyList<Hint>? hints)
    {
        var search = new Search(
            id,
            challengeId,
            NormalizeOrder(order),
            DomainText.NormalizeRequired(clue, "search_clue_required", "Search clue is required.", 1_024),
            DomainText.NormalizeRequired(expectedQrHash, "search_expected_qr_hash_required", "Search expected QR hash is required.", 256),
            NormalizeDifficultyOverride(difficultyOverride),
            NormalizeTimeLimitOverride(timeLimitMinutesOverride));

        search.SetHints(hints ?? Array.Empty<Hint>());

        return search;
    }

    private void SetHints(IReadOnlyList<Hint> hints)
    {
        var ordered = hints.OrderBy(hint => hint.Order).ToList();

        var duplicateOrder = ordered
            .GroupBy(hint => hint.Order)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateOrder is not null)
        {
            throw new UmbralDomainException(
                "search_hint_order_duplicate",
                $"Hint order '{duplicateOrder.Key}' is duplicated within the search.",
                UmbralFailureCategory.Validation);
        }

        _hints.Clear();
        _hints.AddRange(ordered);
    }
}
