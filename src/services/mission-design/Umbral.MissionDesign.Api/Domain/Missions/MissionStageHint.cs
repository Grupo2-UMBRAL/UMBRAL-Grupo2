using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Domain.Missions;

public sealed class MissionStageHint
{
    private MissionStageHint()
    {
    }

    private MissionStageHint(
        Guid id,
        Guid missionStageId,
        string content,
        bool isSolution,
        double? latitude,
        double? longitude)
    {
        Id = id;
        MissionStageId = missionStageId;
        Content = content;
        IsSolution = isSolution;
        Latitude = latitude;
        Longitude = longitude;
    }

    public Guid Id { get; private set; }

    public Guid MissionStageId { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public bool IsSolution { get; private set; }

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    public static MissionStageHint Create(
        Guid id,
        Guid missionStageId,
        string content,
        bool isSolution,
        double? latitude,
        double? longitude)
    {
        var normalizedContent = NormalizeRequiredText(
            content,
            "mission_stage_hint_content_required",
            "Hint content is required.",
            1_024);

        if (latitude.HasValue != longitude.HasValue)
        {
            throw new UmbralDomainException(
                "mission_stage_hint_coordinates_incomplete",
                "Hint coordinates must include both latitude and longitude or neither.",
                UmbralFailureCategory.Validation);
        }

        if (latitude.HasValue)
        {
            var normalizedLatitude = latitude.Value;
            var normalizedLongitude = longitude!.Value;
            if (normalizedLatitude < -90 || normalizedLatitude > 90 || normalizedLongitude < -180 || normalizedLongitude > 180)
            {
                throw new UmbralDomainException(
                    "mission_stage_hint_coordinates_invalid",
                    "Hint coordinates are out of range.",
                    UmbralFailureCategory.Validation);
            }
        }

        return new MissionStageHint(id, missionStageId, normalizedContent, isSolution, latitude, longitude);
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
}
