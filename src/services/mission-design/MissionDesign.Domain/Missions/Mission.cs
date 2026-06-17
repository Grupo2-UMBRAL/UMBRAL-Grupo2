using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using Umbral.ServiceDefaults;

namespace MissionDesign.Domain.Missions;

public sealed class Mission
{
    private static readonly JsonSerializerOptions TreeSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private Mission()
    {
    }

    private Mission(
        Guid id,
        string name,
        string description,
        string difficulty,
        int maximumDurationMinutes,
        string gameType,
        bool isActive,
        string nodeTreeJson)
    {
        Id = id;
        Name = name;
        Description = description;
        Difficulty = difficulty;
        MaximumDurationMinutes = maximumDurationMinutes;
        GameType = gameType;
        IsActive = isActive;
        NodeTreeJson = nodeTreeJson;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string Difficulty { get; private set; } = string.Empty;

    public int MaximumDurationMinutes { get; private set; }

    public string GameType { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public string NodeTreeJson { get; private set; } = "[]";

    [JsonIgnore]
    [NotMapped]
    public IReadOnlyList<MissionNode> Nodes => DeserializeNodes(NodeTreeJson);

    public static Mission Create(
        Guid id,
        string name,
        string description,
        string difficulty,
        int maximumDurationMinutes,
        string gameType,
        IReadOnlyList<MissionNode>? nodes = null)
    {
        var mission = new Mission(
            id,
            NormalizeRequiredText(name, "mission_name_required", "Mission name is required.", 120),
            NormalizeRequiredText(description, "mission_description_required", "Mission description is required.", 1_024),
            NormalizeRequiredText(difficulty, "mission_difficulty_required", "Mission difficulty is required.", 60),
            NormalizeMaximumDuration(maximumDurationMinutes),
            MissionGameType.Normalize(gameType),
            false,
            "[]");

        mission.ReplaceNodes(nodes ?? Array.Empty<MissionNode>());

        return mission;
    }

    public void UpdateDetails(
        string name,
        string description,
        string difficulty,
        int maximumDurationMinutes)
    {
        Name = NormalizeRequiredText(name, "mission_name_required", "Mission name is required.", 120);
        Description = NormalizeRequiredText(description, "mission_description_required", "Mission description is required.", 1_024);
        Difficulty = NormalizeRequiredText(difficulty, "mission_difficulty_required", "Mission difficulty is required.", 60);
        MaximumDurationMinutes = NormalizeMaximumDuration(maximumDurationMinutes);
    }

    public void UpdateCatalogGameType(string gameType)
    {
        GameType = MissionGameType.Normalize(gameType);
    }

    public void ReplaceNodes(IReadOnlyList<MissionNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var normalizedNodes = MissionNode.NormalizeRoots(nodes, MaximumDurationMinutes);
        NodeTreeJson = JsonSerializer.Serialize(normalizedNodes, TreeSerializerOptions);
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
        var activeMissionStages = EnumerateActiveMissionStages(Nodes, MaximumDurationMinutes).ToArray();
        if (activeMissionStages.Length == 0)
        {
            throw new UmbralDomainException(
                "mission_eligible_stage_required",
                "Mission must expose at least one active Mission Stage to be eligible for LiveSession.",
                UmbralFailureCategory.Validation);
        }

        foreach (var missionStage in activeMissionStages)
        {
            _ = missionStage.GetRequiredDifficulty();
            EnsureMissionStagePrompt(missionStage);
            EnsureMissionStageValidationData(missionStage);
            EnsureMissionStageHintsAreConsistent(missionStage);
        }
    }

    public bool IsEligibleForLiveSession()
    {
        try
        {
            EnsureEligibleForLiveSession();
            return true;
        }
        catch (UmbralDomainException)
        {
            return false;
        }
    }

    private static IReadOnlyList<MissionNode> DeserializeNodes(string? nodeTreeJson)
    {
        if (string.IsNullOrWhiteSpace(nodeTreeJson))
        {
            return Array.Empty<MissionNode>();
        }

        IReadOnlyList<MissionNode>? nodes =
            JsonSerializer.Deserialize<List<MissionNode>>(nodeTreeJson, TreeSerializerOptions);

        return nodes ?? Array.Empty<MissionNode>();
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

    private static IEnumerable<MissionNode> EnumerateActiveMissionStages(
        IEnumerable<MissionNode> nodes,
        int inheritedTimeBudgetMinutes)
    {
        foreach (var node in nodes)
        {
            if (!node.IsActive)
            {
                continue;
            }

            var resolvedTimeBudgetMinutes = node.ResolveTimeBudgetMinutes(inheritedTimeBudgetMinutes);
            if (node.IsLeaf)
            {
                yield return node;
                continue;
            }

            foreach (var child in EnumerateActiveMissionStages(node.Children, resolvedTimeBudgetMinutes))
            {
                yield return child;
            }
        }
    }

    private static void EnsureMissionStageValidationData(MissionNode missionStage)
    {
        if (string.Equals(missionStage.GameType, MissionGameType.TreasureHunt, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(missionStage.ExpectedQrHash))
            {
                throw new UmbralDomainException(
                    "mission_eligible_stage_expected_qr_hash_required",
                    $"Mission Stage '{missionStage.Name}' must define an expected QR hash.",
                    UmbralFailureCategory.Validation);
            }

            return;
        }

        if (string.Equals(missionStage.GameType, MissionGameType.Trivia, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(missionStage.TriviaValidAnswer) &&
                string.IsNullOrWhiteSpace(missionStage.TriviaInitialValidationCriterion))
            {
                throw new UmbralDomainException(
                    "mission_eligible_stage_trivia_validation_required",
                    $"Mission Stage '{missionStage.Name}' must define a valid answer or validation criterion.",
                    UmbralFailureCategory.Validation);
            }

            return;
        }

        throw new UmbralDomainException(
            "mission_eligible_stage_game_type_unsupported",
            $"Mission Stage '{missionStage.Name}' has unsupported Game Type '{missionStage.GameType}'.",
            UmbralFailureCategory.Validation);
    }

    private static void EnsureMissionStagePrompt(MissionNode missionStage)
    {
        if (!string.IsNullOrWhiteSpace(missionStage.Prompt))
        {
            return;
        }

        throw new UmbralDomainException(
            "mission_eligible_stage_prompt_required",
            $"Mission Stage '{missionStage.Name}' must define a prompt.",
            UmbralFailureCategory.Validation);
    }

    private static void EnsureMissionStageHintsAreConsistent(MissionNode missionStage)
    {
        foreach (var hint in missionStage.Hints)
        {
            if (string.IsNullOrWhiteSpace(hint.Content))
            {
                throw new UmbralDomainException(
                    "mission_eligible_hint_content_required",
                    $"Mission Stage '{missionStage.Name}' has a hint without content.",
                    UmbralFailureCategory.Validation);
            }

            if (hint.Latitude.HasValue != hint.Longitude.HasValue)
            {
                throw new UmbralDomainException(
                    "mission_eligible_hint_coordinates_incomplete",
                    $"Mission Stage '{missionStage.Name}' has a hint with incomplete coordinates.",
                    UmbralFailureCategory.Validation);
            }
        }
    }
}
