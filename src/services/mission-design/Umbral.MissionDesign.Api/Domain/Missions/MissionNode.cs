using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Domain.Missions;

public sealed record MissionNode
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public int Order { get; init; }

    public bool IsActive { get; init; } = true;

    public int? DefaultTimeBudgetMinutes { get; init; }

    public int? TimeBudgetMinutes { get; init; }

    public string? Difficulty { get; init; }

    public string? GameType { get; init; }

    public string? Prompt { get; init; }

    public string? ExpectedQrHash { get; init; }

    public string? TriviaValidAnswer { get; init; }

    public string? TriviaInitialValidationCriterion { get; init; }

    public IReadOnlyList<MissionHint> Hints { get; init; } = Array.Empty<MissionHint>();

    public IReadOnlyList<MissionNode> Children { get; init; } = Array.Empty<MissionNode>();

    public bool IsLeaf => Children.Count == 0;

    public static MissionNode Create(
        Guid id,
        string name,
        int order,
        bool isActive,
        int? defaultTimeBudgetMinutes = null,
        int? timeBudgetMinutes = null,
        string? difficulty = null,
        string? gameType = null,
        string? prompt = null,
        string? expectedQrHash = null,
        string? triviaValidAnswer = null,
        string? triviaInitialValidationCriterion = null,
        IReadOnlyList<MissionHint>? hints = null,
        IReadOnlyList<MissionNode>? children = null)
    {
        var normalizedName = NormalizeRequiredText(name, "mission_node_name_required", "Mission node name is required.", 120);
        if (order <= 0)
        {
            throw new UmbralDomainException(
                "mission_node_order_invalid",
                "Mission node order must be greater than zero.",
                UmbralFailureCategory.Validation);
        }

        NormalizeBudget(defaultTimeBudgetMinutes, "mission_node_default_time_budget_invalid", "Default time budget must be greater than zero when provided.");
        NormalizeBudget(timeBudgetMinutes, "mission_node_time_budget_invalid", "Time budget must be greater than zero when provided.");

        var normalizedChildren = NormalizeChildren(children);
        var normalizedHints = NormalizeHints(hints);

        if (normalizedChildren.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(difficulty) ||
                !string.IsNullOrWhiteSpace(gameType) ||
                !string.IsNullOrWhiteSpace(prompt) ||
                !string.IsNullOrWhiteSpace(expectedQrHash) ||
                !string.IsNullOrWhiteSpace(triviaValidAnswer) ||
                !string.IsNullOrWhiteSpace(triviaInitialValidationCriterion) ||
                timeBudgetMinutes.HasValue ||
                normalizedHints.Count > 0)
            {
                throw new UmbralDomainException(
                    "mission_node_composite_invalid_payload",
                    "Composite mission nodes cannot define leaf-only game validation data or hints.",
                    UmbralFailureCategory.Validation);
            }
        }
        else
        {
            if (defaultTimeBudgetMinutes.HasValue)
            {
                throw new UmbralDomainException(
                    "mission_node_leaf_default_time_budget_not_allowed",
                    "Leaf mission nodes must use TimeBudgetMinutes for explicit time budget values.",
                    UmbralFailureCategory.Validation);
            }

            var normalizedDifficulty = MissionStageDifficulty.Normalize(difficulty);
            var normalizedGameType = MissionGameType.Normalize(gameType ?? string.Empty);
            var normalizedPrompt = NormalizeRequiredText(
                prompt ?? string.Empty,
                "mission_node_prompt_required",
                "Mission stage prompt is required.",
                1_024);
            var normalizedValidation = NormalizeLeafValidationData(
                normalizedGameType,
                expectedQrHash,
                triviaValidAnswer,
                triviaInitialValidationCriterion);

            return new MissionNode
            {
                Id = id,
                Name = normalizedName,
                Order = order,
                IsActive = isActive,
                TimeBudgetMinutes = timeBudgetMinutes,
                Difficulty = normalizedDifficulty,
                GameType = normalizedGameType,
                Prompt = normalizedPrompt,
                ExpectedQrHash = normalizedValidation.ExpectedQrHash,
                TriviaValidAnswer = normalizedValidation.TriviaValidAnswer,
                TriviaInitialValidationCriterion = normalizedValidation.TriviaInitialValidationCriterion,
                Hints = normalizedHints,
                Children = normalizedChildren
            };
        }

        return new MissionNode
        {
            Id = id,
            Name = normalizedName,
            Order = order,
            IsActive = isActive,
            DefaultTimeBudgetMinutes = defaultTimeBudgetMinutes,
            Hints = normalizedHints,
            Children = normalizedChildren
        };
    }

    public int ResolveTimeBudgetMinutes(int inheritedTimeBudgetMinutes)
    {
        return TimeBudgetMinutes ?? DefaultTimeBudgetMinutes ?? inheritedTimeBudgetMinutes;
    }

    public string GetRequiredDifficulty()
    {
        if (!IsLeaf)
        {
            throw new UmbralDomainException(
                "mission_node_composite_difficulty_not_allowed",
                "Composite mission nodes cannot define difficulty.",
                UmbralFailureCategory.Validation);
        }

        return MissionStageDifficulty.Normalize(Difficulty);
    }

    public static IReadOnlyList<MissionNode> NormalizeRoots(
        IReadOnlyList<MissionNode> nodes,
        int inheritedTimeBudgetMinutes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var normalizedNodes = NormalizeChildren(nodes);
        foreach (var node in normalizedNodes)
        {
            ValidateTree(node, inheritedTimeBudgetMinutes);
        }

        return normalizedNodes;
    }

    private static void ValidateTree(MissionNode node, int inheritedTimeBudgetMinutes)
    {
        var resolvedTimeBudgetMinutes = node.ResolveTimeBudgetMinutes(inheritedTimeBudgetMinutes);
        if (resolvedTimeBudgetMinutes <= 0)
        {
            throw new UmbralDomainException(
                "mission_node_time_budget_required",
                "Every mission node leaf must resolve to a positive time budget.",
                UmbralFailureCategory.Validation);
        }

        if (node.IsLeaf)
        {
            return;
        }

        foreach (var child in node.Children)
        {
            ValidateTree(child, resolvedTimeBudgetMinutes);
        }
    }

    private static IReadOnlyList<MissionNode> NormalizeChildren(IReadOnlyList<MissionNode>? nodes)
    {
        if (nodes is null || nodes.Count == 0)
        {
            return Array.Empty<MissionNode>();
        }

        var orderedNodes = nodes
            .OrderBy(node => node.Order)
            .ToList();

        var duplicateOrder = orderedNodes
            .GroupBy(node => node.Order)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateOrder is not null)
        {
            throw new UmbralDomainException(
                "mission_node_order_duplicate",
                $"Mission node order '{duplicateOrder.Key}' is duplicated among siblings.",
                UmbralFailureCategory.Validation);
        }

        return orderedNodes;
    }

    private static IReadOnlyList<MissionHint> NormalizeHints(IReadOnlyList<MissionHint>? hints)
    {
        if (hints is null || hints.Count == 0)
        {
            return Array.Empty<MissionHint>();
        }

        return hints.ToArray();
    }

    private static (string? ExpectedQrHash, string? TriviaValidAnswer, string? TriviaInitialValidationCriterion) NormalizeLeafValidationData(
        string gameType,
        string? expectedQrHash,
        string? triviaValidAnswer,
        string? triviaInitialValidationCriterion)
    {
        if (string.Equals(gameType, MissionGameType.TreasureHunt, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(expectedQrHash))
            {
                throw new UmbralDomainException(
                    "mission_node_expected_qr_hash_required",
                    "Treasure Hunt mission nodes must define the expected QR hash.",
                    UmbralFailureCategory.Validation);
            }

            return (NormalizeRequiredText(expectedQrHash, "mission_node_expected_qr_hash_required", "Expected QR hash is required.", 256), null, null);
        }

        if (string.Equals(gameType, MissionGameType.Trivia, StringComparison.OrdinalIgnoreCase))
        {
            var normalizedTriviaValidAnswer = NormalizeOptionalText(
                triviaValidAnswer,
                "mission_node_trivia_valid_answer",
                512);
            var normalizedTriviaInitialValidationCriterion = NormalizeOptionalText(
                triviaInitialValidationCriterion,
                "mission_node_trivia_initial_validation_criterion",
                512);

            if (normalizedTriviaValidAnswer is null && normalizedTriviaInitialValidationCriterion is null)
            {
                throw new UmbralDomainException(
                    "mission_node_trivia_validation_required",
                    "Trivia mission nodes must define a valid answer or an initial validation criterion.",
                    UmbralFailureCategory.Validation);
            }

            return (null, normalizedTriviaValidAnswer, normalizedTriviaInitialValidationCriterion);
        }

        throw new UmbralDomainException(
            "mission_node_game_type_unsupported",
            $"Game Type '{gameType}' is not supported for mission nodes.",
            UmbralFailureCategory.Validation);
    }

    private static string NormalizeRequiredText(
        string value,
        string errorCode,
        string errorMessage,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UmbralDomainException(
                errorCode,
                errorMessage,
                UmbralFailureCategory.Validation);
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new UmbralDomainException(
                $"{errorCode}_too_long",
                $"Value cannot exceed {maximumLength} characters.",
                UmbralFailureCategory.Validation);
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(string? value, string errorCode, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new UmbralDomainException(
                $"{errorCode}_too_long",
                $"Value cannot exceed {maximumLength} characters.",
                UmbralFailureCategory.Validation);
        }

        return normalized;
    }

    private static void NormalizeBudget(int? budgetMinutes, string errorCode, string errorMessage)
    {
        if (budgetMinutes is null)
        {
            return;
        }

        if (budgetMinutes <= 0)
        {
            throw new UmbralDomainException(
                errorCode,
                errorMessage,
                UmbralFailureCategory.Validation);
        }
    }
}
