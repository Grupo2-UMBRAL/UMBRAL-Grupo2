using Umbral.ServiceDefaults;

namespace MissionManagement.Domain.Missions;

/// <summary>
/// Aggregate root. A Mission owns an ordered list of root <see cref="PathItem"/>s (the Composite).
/// Its playable path is the depth-first, in-order flatten of the <see cref="Play"/>s of active
/// <see cref="Challenge"/>s. The Mission no longer authors Game Type or Difficulty; both are derived.
/// </summary>
public sealed class Mission
{
    private readonly List<PathItem> _rootItems = new();

    private Mission()
    {
    }

    private Mission(
        Guid id,
        string name,
        string description,
        int maximumDurationMinutes,
        bool isActive)
    {
        Id = id;
        Name = name;
        Description = description;
        MaximumDurationMinutes = maximumDurationMinutes;
        IsActive = isActive;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int MaximumDurationMinutes { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>The ordered root Path Items (those whose <see cref="PathItem.ParentSectionId"/> is null).</summary>
    public IReadOnlyList<PathItem> RootItems => _rootItems;

    public static Mission Create(
        Guid id,
        string name,
        string description,
        int maximumDurationMinutes,
        IReadOnlyList<PathItem>? rootItems = null)
    {
        var mission = new Mission(
            id,
            DomainText.NormalizeRequired(name, "mission_name_required", "Mission name is required.", 120),
            DomainText.NormalizeRequired(description, "mission_description_required", "Mission description is required.", 1_024),
            NormalizeMaximumDuration(maximumDurationMinutes),
            false);

        mission.SetRootItems(rootItems ?? Array.Empty<PathItem>());

        return mission;
    }

    public void UpdateDetails(string name, string description, int maximumDurationMinutes)
    {
        Name = DomainText.NormalizeRequired(name, "mission_name_required", "Mission name is required.", 120);
        Description = DomainText.NormalizeRequired(description, "mission_description_required", "Mission description is required.", 1_024);
        MaximumDurationMinutes = NormalizeMaximumDuration(maximumDurationMinutes);
    }

    public void ReplaceItems(IReadOnlyList<PathItem> rootItems)
    {
        ArgumentNullException.ThrowIfNull(rootItems);
        SetRootItems(rootItems);
    }

    /// <summary>
    /// Rebuilds a Mission aggregate from flat persistence rows. Plays are attached to their Challenge,
    /// choices/hints to their play, and path items are linked by <see cref="PathItem.ParentSectionId"/>
    /// into the Section tree, everything ordered by <see cref="PathItem.Order"/> / play order.
    /// </summary>
    public static Mission Rehydrate(
        Guid id,
        string name,
        string description,
        int maximumDurationMinutes,
        bool isActive,
        IReadOnlyList<Section> sections,
        IReadOnlyList<Challenge> challenges,
        IReadOnlyList<Play> plays,
        IReadOnlyList<Choice> choices,
        IReadOnlyList<Hint> hints)
    {
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(challenges);
        ArgumentNullException.ThrowIfNull(plays);
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentNullException.ThrowIfNull(hints);

        var mission = new Mission(id, name, description, maximumDurationMinutes, isActive);

        // choices -> questions, hints -> searches
        var choicesByQuestion = choices.ToLookup(choice => choice.QuestionId);
        foreach (var question in plays.OfType<Question>())
        {
            foreach (var choice in choicesByQuestion[question.Id].OrderBy(choice => choice.Order))
            {
                question.AttachChoice(choice);
            }

            question.SortChoices();
        }

        var hintsBySearch = hints.ToLookup(hint => hint.SearchId);
        foreach (var search in plays.OfType<Search>())
        {
            foreach (var hint in hintsBySearch[search.Id].OrderBy(hint => hint.Order))
            {
                search.AttachHint(hint);
            }

            search.SortHints();
        }

        // plays -> challenges
        var playsByChallenge = plays.ToLookup(play => play.ChallengeId);
        foreach (var challenge in challenges)
        {
            foreach (var play in playsByChallenge[challenge.Id].OrderBy(play => play.Order))
            {
                challenge.AttachPlay(play);
            }

            challenge.SortPlays();
        }

        // path items -> section tree
        var pathItems = sections.Cast<PathItem>().Concat(challenges).ToList();
        var sectionsById = sections.ToDictionary(section => section.Id);

        foreach (var item in pathItems)
        {
            if (item.ParentSectionId is { } parentId && sectionsById.TryGetValue(parentId, out var parent))
            {
                parent.AttachChild(item);
            }
            else
            {
                mission._rootItems.Add(item);
            }
        }

        foreach (var section in sections)
        {
            section.SortChildren();
        }

        mission._rootItems.Sort(static (left, right) => left.Order.CompareTo(right.Order));

        return mission;
    }

    /// <summary>
    /// Builds a Mission from an already-constructed root <see cref="PathItem"/> tree (children already
    /// attached), preserving <paramref name="isActive"/>. Used to view/validate a post-update aggregate
    /// without round-tripping through persistence.
    /// </summary>
    public static Mission RehydrateTree(
        Guid id,
        string name,
        string description,
        int maximumDurationMinutes,
        bool isActive,
        IReadOnlyList<PathItem> rootItems)
    {
        ArgumentNullException.ThrowIfNull(rootItems);

        var mission = new Mission(id, name, description, maximumDurationMinutes, isActive);
        mission.SetRootItems(rootItems);

        return mission;
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            throw new UmbralDomainException(
                "mission_already_inactive",
                "Mission is already inactive.",
                UmbralFailureCategory.Conflict);
        }

        IsActive = false;
    }

    public void Activate()
    {
        if (IsActive)
        {
            throw new UmbralDomainException(
                "mission_already_active",
                "Mission is already active.",
                UmbralFailureCategory.Conflict);
        }

        EnsureEligibleForLiveSession();
        IsActive = true;
    }

    public void EnsureEligibleForLiveSession()
    {
        if (Flatten().Count == 0)
        {
            throw new UmbralDomainException(
                "mission_eligible_play_required",
                "Mission must expose at least one play from an active, well-formed Challenge to be eligible for LiveSession.",
                UmbralFailureCategory.Validation);
        }
    }

    public bool IsEligibleForLiveSession()
    {
        return Flatten().Count > 0;
    }

    /// <summary>
    /// Depth-first, in-order flatten over root Path Items ordered by Order, descending into Sections,
    /// collecting the Plays (in play Order) of active, well-formed Challenges only. A global 1-based
    /// Order index is assigned across the whole flattened sequence.
    /// </summary>
    public IReadOnlyList<FlattenedPlay> Flatten()
    {
        var result = new List<FlattenedPlay>();
        FlattenInto(_rootItems, result);
        return result;
    }

    private static void FlattenInto(IReadOnlyList<PathItem> items, List<FlattenedPlay> result)
    {
        foreach (var item in items.OrderBy(item => item.Order))
        {
            switch (item)
            {
                case Section section:
                    FlattenInto(section.Children, result);
                    break;

                case Challenge { IsActive: true } challenge when challenge.IsWellFormed:
                    foreach (var play in challenge.Plays.OrderBy(play => play.Order))
                    {
                        result.Add(Project(challenge, play, result.Count + 1));
                    }

                    break;
            }
        }
    }

    private static FlattenedPlay Project(Challenge challenge, Play play, int globalOrder)
    {
        var difficulty = play.ResolveDifficulty(challenge.DefaultDifficulty);
        var timeLimit = play.ResolveTimeLimitMinutes(challenge.DefaultTimeLimitMinutes);

        return play switch
        {
            Question question => new FlattenedPlay
            {
                Id = question.Id,
                Order = globalOrder,
                GameType = challenge.GameType,
                Difficulty = difficulty,
                TimeLimitMinutes = timeLimit,
                Prompt = question.Prompt,
                Choices = question.Choices
                    .OrderBy(choice => choice.Order)
                    .Select(choice => new FlattenedChoice(choice.Id, choice.Text))
                    .ToArray(),
                CorrectChoiceId = question.Choices.FirstOrDefault(choice => choice.IsCorrect)?.Id
            },
            Search search => new FlattenedPlay
            {
                Id = search.Id,
                Order = globalOrder,
                GameType = challenge.GameType,
                Difficulty = difficulty,
                TimeLimitMinutes = timeLimit,
                Prompt = search.Prompt,
                ExpectedQrHash = search.ExpectedQrHash,
                Hints = search.Hints
                    .OrderBy(hint => hint.Order)
                    .Select(hint => new FlattenedHint(hint.Content, hint.IsSolution, hint.Latitude, hint.Longitude))
                    .ToArray()
            },
            _ => throw new UmbralDomainException(
                "play_kind_unsupported",
                $"Play '{play.Id}' has an unsupported kind.",
                UmbralFailureCategory.Validation)
        };
    }

    private void SetRootItems(IReadOnlyList<PathItem> rootItems)
    {
        var ordered = rootItems.OrderBy(item => item.Order).ToList();

        var duplicateOrder = ordered
            .GroupBy(item => item.Order)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateOrder is not null)
        {
            throw new UmbralDomainException(
                "path_item_order_duplicate",
                $"Path item order '{duplicateOrder.Key}' is duplicated among siblings.",
                UmbralFailureCategory.Validation);
        }

        _rootItems.Clear();
        _rootItems.AddRange(ordered);
    }

    private static int NormalizeMaximumDuration(int maximumDurationMinutes)
    {
        if (maximumDurationMinutes <= 0)
        {
            throw new UmbralDomainException(
                "mission_maximum_duration_invalid",
                "Maximum duration must be greater than zero minutes.",
                UmbralFailureCategory.Validation);
        }

        if (maximumDurationMinutes > 1_440)
        {
            throw new UmbralDomainException(
                "mission_maximum_duration_too_large",
                "Maximum duration cannot exceed 1440 minutes.",
                UmbralFailureCategory.Validation);
        }

        return maximumDurationMinutes;
    }
}
