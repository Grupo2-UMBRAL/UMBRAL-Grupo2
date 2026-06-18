using Umbral.ServiceDefaults;

namespace MissionManagement.Domain.Missions;

public sealed record MissionHint
{
    public Guid Id { get; init; }

    public string Content { get; init; } = string.Empty;

    public bool IsSolution { get; init; }

    public decimal? Latitude { get; init; }

    public decimal? Longitude { get; init; }

    public static MissionHint Create(
        Guid id,
        string content,
        bool isSolution,
        decimal? latitude = null,
        decimal? longitude = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new UmbralDomainException(
                "mission_hint_content_required",
                "Hint content is required.",
                UmbralFailureCategory.Validation);
        }

        var normalizedContent = content.Trim();
        if (normalizedContent.Length > 1_024)
        {
            throw new UmbralDomainException(
                "mission_hint_content_too_long",
                "Hint content cannot exceed 1024 characters.",
                UmbralFailureCategory.Validation);
        }

        EnsureCoordinatesAreComplete(latitude, longitude);

        return new MissionHint
        {
            Id = id,
            Content = normalizedContent,
            IsSolution = isSolution,
            Latitude = latitude,
            Longitude = longitude
        };
    }

    private static void EnsureCoordinatesAreComplete(decimal? latitude, decimal? longitude)
    {
        if (latitude.HasValue == longitude.HasValue)
        {
            return;
        }

        throw new UmbralDomainException(
            "mission_hint_coordinates_incomplete",
            "Hint coordinates must include both latitude and longitude or neither.",
            UmbralFailureCategory.Validation);
    }
}
