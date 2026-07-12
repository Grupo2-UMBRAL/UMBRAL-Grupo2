using Umbral.ServiceDefaults;

namespace MissionManagement.Domain.Missions;

public static class MissionGameType
{
    public const string TreasureHunt = "Treasure Hunt";
    public const string Trivia = "Trivia";

    private static readonly string[] SupportedValues =
    [
        TreasureHunt,
        Trivia
    ];

    public static IReadOnlyList<string> GetSupportedValues() => SupportedValues;

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UmbralDomainException(
                "mission_game_type_required",
                "Game Type is required.",
                UmbralFailureCategory.Validation);
        }

        var normalized = SupportedValues.SingleOrDefault(
            supportedValue => string.Equals(
                supportedValue,
                value.Trim(),
                StringComparison.OrdinalIgnoreCase));

        if (normalized is null)
        {
            throw new UmbralDomainException(
                "mission_game_type_unsupported",
                $"Game Type '{value}' is not supported. Use Treasure Hunt or Trivia.",
                UmbralFailureCategory.Validation);
        }

        return normalized;
    }
}
