using Umbral.ServiceDefaults;

namespace ScoringMonitoring.Domain.Audit;

public sealed class SessionEventLog
{
    public const int EventTypeMaxLength = 80;
    public const int DescriptionMaxLength = 1000;

    private SessionEventLog()
    {
        EventType = string.Empty;
        Description = string.Empty;
    }

    public SessionEventLog(
        Guid id,
        Guid liveSessionId,
        string eventType,
        string description,
        DateTimeOffset timestamp)
    {
        if (id == Guid.Empty)
        {
            throw new UmbralDomainException("session_event_log.empty_id", "Session Event Log id is required.");
        }

        if (liveSessionId == Guid.Empty)
        {
            throw new UmbralDomainException(
                "session_event_log.empty_live_session_id",
                "LiveSession id is required.");
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new UmbralDomainException(
                "session_event_log.empty_event_type",
                "Session Event Log type is required.");
        }

        if (eventType.Length > EventTypeMaxLength)
        {
            throw new UmbralDomainException(
                "session_event_log.event_type_too_long",
                "Session Event Log type is too long.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new UmbralDomainException(
                "session_event_log.empty_description",
                "Session Event Log description is required.");
        }

        if (description.Length > DescriptionMaxLength)
        {
            throw new UmbralDomainException(
                "session_event_log.description_too_long",
                "Session Event Log description is too long.");
        }

        Id = id;
        LiveSessionId = liveSessionId;
        EventType = eventType.Trim();
        Description = description.Trim();
        Timestamp = timestamp;
    }

    public Guid Id { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public string EventType { get; private set; }

    public string Description { get; private set; }

    public DateTimeOffset Timestamp { get; private set; }
}
