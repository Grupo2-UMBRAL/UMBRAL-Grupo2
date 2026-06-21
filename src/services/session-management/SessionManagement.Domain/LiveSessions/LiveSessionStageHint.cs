using Umbral.ServiceDefaults;

namespace SessionManagement.Domain.LiveSessions;

public sealed record LiveSessionStageHint
{
    public Guid Id { get; init; }

    public string Content { get; init; } = string.Empty;

    public bool IsSolution { get; init; }

    public decimal? Latitude { get; init; }

    public decimal? Longitude { get; init; }

    public static LiveSessionStageHint Create(
        Guid id,
        string content,
        bool isSolution,
        decimal? latitude = null,
        decimal? longitude = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new UmbralDomainException(
                "live_session_stage_hint_content_required",
                "LiveSession stage hint content is required.",
                UmbralFailureCategory.Validation);
        }

        if (latitude.HasValue != longitude.HasValue)
        {
            throw new UmbralDomainException(
                "live_session_stage_hint_coordinates_incomplete",
                "LiveSession stage hint coordinates must include latitude and longitude together.",
                UmbralFailureCategory.Validation);
        }

        return new LiveSessionStageHint
        {
            Id = id,
            Content = content.Trim(),
            IsSolution = isSolution,
            Latitude = latitude,
            Longitude = longitude
        };
    }
}
