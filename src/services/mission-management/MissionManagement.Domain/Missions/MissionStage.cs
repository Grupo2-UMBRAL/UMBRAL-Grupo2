using Umbral.ServiceDefaults;

namespace MissionManagement.Domain.Missions;

public sealed class MissionStage
{
    private MissionStage()
    {
    }

    private MissionStage(
        Guid id,
        Guid missionId,
        string name,
        int order,
        string difficulty,
        string gameType,
        string? expectedQrHash,
        string? triviaValidationCriteria,
        bool isActive)
    {
        Id = id;
        MissionId = missionId;
        Name = name;
        Order = order;
        Difficulty = difficulty;
        GameType = gameType;
        ExpectedQrHash = expectedQrHash;
        TriviaValidationCriteria = triviaValidationCriteria;
        IsActive = isActive;
    }

    public Guid Id { get; private set; }

    public Guid MissionId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int Order { get; private set; }

    public string Difficulty { get; private set; } = string.Empty;

    public string GameType { get; private set; } = string.Empty;

    public string? ExpectedQrHash { get; private set; }

    public string? TriviaValidationCriteria { get; private set; }

    public bool IsActive { get; private set; }

    public ICollection<MissionStageHint> Hints { get; } = new List<MissionStageHint>();

    public static MissionStage Create(
        Guid id,
        Guid missionId,
        string name,
        int order,
        string difficulty,
        string gameType,
        string? expectedQrHash,
        string? triviaValidationCriteria)
    {
        var normalizedName = NormalizeRequiredText(name, "mission_stage_name_required", "Mission Stage name is required.", 120);
        var normalizedOrder = NormalizeOrder(order);
        var normalizedDifficulty = MissionStageDifficulty.Normalize(difficulty);
        var normalizedGameType = MissionGameType.Normalize(gameType);
        var normalizedExpectedQrHash = NormalizeOptionalText(expectedQrHash, "mission_stage_expected_qr_hash_too_long", 256);
        var normalizedTriviaValidationCriteria = NormalizeOptionalText(triviaValidationCriteria, "mission_stage_trivia_validation_criteria_too_long", 1_024);

        if (normalizedGameType == MissionGameType.TreasureHunt)
        {
            if (!string.IsNullOrWhiteSpace(triviaValidationCriteria))
            {
                throw new UmbralDomainException(
                    "mission_stage_trivia_validation_criteria_not_applicable",
                    "Treasure Hunt Mission Stages cannot define a trivia validation criterion.",
                    UmbralFailureCategory.Validation);
            }

            normalizedExpectedQrHash = NormalizeRequiredText(
                expectedQrHash,
                "mission_stage_expected_qr_hash_required",
                "Treasure Hunt Mission Stages require an expected QR hash.",
                256);
            normalizedTriviaValidationCriteria = null;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(expectedQrHash))
            {
                throw new UmbralDomainException(
                    "mission_stage_expected_qr_hash_not_applicable",
                    "Trivia Mission Stages cannot define an expected QR hash.",
                    UmbralFailureCategory.Validation);
            }

            normalizedTriviaValidationCriteria = NormalizeRequiredText(
                triviaValidationCriteria,
                "mission_stage_trivia_validation_criteria_required",
                "Trivia Mission Stages require a validation criterion.",
                1_024);
            normalizedExpectedQrHash = null;
        }

        return new MissionStage(
            id,
            missionId,
            normalizedName,
            normalizedOrder,
            normalizedDifficulty,
            normalizedGameType,
            normalizedExpectedQrHash,
            normalizedTriviaValidationCriteria,
            true);
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            throw new UmbralDomainException(
                "mission_stage_already_inactive",
                "Mission Stage is already inactive.",
                UmbralFailureCategory.Conflict);
        }

        IsActive = false;
    }

    public MissionStageHint AddHint(
        Guid hintId,
        string content,
        bool isSolution,
        double? latitude,
        double? longitude)
    {
        var hint = MissionStageHint.Create(hintId, Id, content, isSolution, latitude, longitude);
        Hints.Add(hint);

        return hint;
    }

    private static string NormalizeRequiredText(
        string? value,
        string errorCode,
        string errorMessage,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UmbralDomainException(errorCode, errorMessage, UmbralFailureCategory.Validation);
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

    private static string? NormalizeOptionalText(
        string? value,
        string errorCode,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new UmbralDomainException(
                errorCode,
                $"Value cannot exceed {maximumLength} characters.",
                UmbralFailureCategory.Validation);
        }

        return normalized;
    }

    private static int NormalizeOrder(int order)
    {
        if (order <= 0)
        {
            throw new UmbralDomainException(
                "mission_stage_order_invalid",
                "Mission Stage order must be greater than zero.",
                UmbralFailureCategory.Validation);
        }

        return order;
    }
}
