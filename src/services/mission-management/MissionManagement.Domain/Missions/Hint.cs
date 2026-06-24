using Umbral.ServiceDefaults;

namespace MissionManagement.Domain.Missions;

public sealed class Hint
{
    private Hint()
    {
    }

    private Hint(
        Guid id,
        Guid searchId,
        int order,
        string content,
        bool isSolution,
        double? latitude,
        double? longitude)
    {
        Id = id;
        SearchId = searchId;
        Order = order;
        Content = content;
        IsSolution = isSolution;
        Latitude = latitude;
        Longitude = longitude;
    }

    public Guid Id { get; private set; }

    public Guid SearchId { get; private set; }

    public int Order { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public bool IsSolution { get; private set; }

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    public static Hint Create(
        Guid id,
        Guid searchId,
        int order,
        string content,
        bool isSolution,
        double? latitude,
        double? longitude)
    {
        var normalizedOrder = DomainText.NormalizeOrder(
            order,
            "hint_order_invalid",
            "Hint order must be greater than zero.");
        var normalizedContent = DomainText.NormalizeRequired(
            content,
            "hint_content_required",
            "Hint content is required.",
            1_024);

        if (latitude.HasValue != longitude.HasValue)
        {
            throw new UmbralDomainException(
                "hint_coordinates_incomplete",
                "Hint coordinates must include both latitude and longitude or neither.",
                UmbralFailureCategory.Validation);
        }

        if (latitude.HasValue)
        {
            var latitudeValue = latitude.Value;
            var longitudeValue = longitude!.Value;
            if (latitudeValue < -90 || latitudeValue > 90 || longitudeValue < -180 || longitudeValue > 180)
            {
                throw new UmbralDomainException(
                    "hint_coordinates_invalid",
                    "Hint coordinates are out of range.",
                    UmbralFailureCategory.Validation);
            }
        }

        return new Hint(id, searchId, normalizedOrder, normalizedContent, isSolution, latitude, longitude);
    }
}
