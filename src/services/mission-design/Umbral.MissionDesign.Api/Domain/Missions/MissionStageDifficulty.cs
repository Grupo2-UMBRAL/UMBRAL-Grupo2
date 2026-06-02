namespace Umbral.MissionDesign.Api.Domain.Missions;

public static class MissionStageDifficulty
{
    public const string Easy = "Easy";
    public const string Medium = "Medium";
    public const string Hard = "Hard";

    public static string Normalize(string? difficulty)
    {
        if (string.IsNullOrWhiteSpace(difficulty))
        {
            throw CreateValidationException("mission_stage_difficulty_required", "Mission Stage difficulty is required.");
        }

        return difficulty.Trim() switch
        {
            Easy => Easy,
            Medium => Medium,
            Hard => Hard,
            _ => throw CreateValidationException(
                "mission_stage_difficulty_invalid",
                "Mission Stage difficulty must be Easy, Medium, or Hard.")
        };
    }

    private static Exception CreateValidationException(string code, string message)
    {
        return new Umbral.ServiceDefaults.UmbralDomainException(
            code,
            message,
            Umbral.ServiceDefaults.UmbralFailureCategory.Validation);
    }
}
