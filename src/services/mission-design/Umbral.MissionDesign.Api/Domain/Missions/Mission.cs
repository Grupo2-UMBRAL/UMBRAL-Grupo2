using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Domain.Missions;

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
            true,
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
}
