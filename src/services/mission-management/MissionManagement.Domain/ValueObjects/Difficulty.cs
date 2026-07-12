namespace MissionManagement.Domain.Missions;

public static class Difficulty
{
    public const string Easy = "Easy";
    public const string Medium = "Medium";
    public const string Hard = "Hard";

    public static string Normalize(string? difficulty)
    {
        if (string.IsNullOrWhiteSpace(difficulty))
        {
            throw CreateValidationException("difficulty_required", "Difficulty is required.");
        }

        return difficulty.Trim() switch
        {
            Easy => Easy,
            Medium => Medium,
            Hard => Hard,
            _ => throw CreateValidationException(
                "difficulty_invalid",
                "Difficulty must be Easy, Medium, or Hard.")
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
