using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Domain.Missions;

public sealed class Mission
{
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
        bool isActive)
    {
        Id = id;
        Name = name;
        Description = description;
        Difficulty = difficulty;
        MaximumDurationMinutes = maximumDurationMinutes;
        GameType = gameType;
        IsActive = isActive;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string Difficulty { get; private set; } = string.Empty;

    public int MaximumDurationMinutes { get; private set; }

    public string GameType { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public static Mission Create(
        Guid id,
        string name,
        string description,
        string difficulty,
        int maximumDurationMinutes,
        string gameType)
    {
        return new Mission(
            id,
            NormalizeRequiredText(name, "mission_name_required", "Mission name is required.", 120),
            NormalizeRequiredText(description, "mission_description_required", "Mission description is required.", 1_024),
            NormalizeRequiredText(difficulty, "mission_difficulty_required", "Mission difficulty is required.", 60),
            NormalizeMaximumDuration(maximumDurationMinutes),
            MissionGameType.Normalize(gameType),
            true);
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
