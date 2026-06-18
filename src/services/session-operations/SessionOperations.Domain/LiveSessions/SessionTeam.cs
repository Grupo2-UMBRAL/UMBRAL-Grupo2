using Umbral.ServiceDefaults;

namespace SessionOperations.Domain.LiveSessions;

public sealed class SessionTeam
{
    public const int NameMaximumLength = 80;

    private SessionTeam()
    {
    }

    private SessionTeam(Guid id, Guid liveSessionId, string name, DateTimeOffset createdAtUtc)
    {
        Id = id;
        LiveSessionId = liveSessionId;
        Name = name;
        NormalizedName = NormalizeTeamName(name);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static SessionTeam Create(Guid liveSessionId, Guid id, string name, DateTimeOffset createdAtUtc)
    {
        if (liveSessionId == Guid.Empty)
        {
            throw new UmbralDomainException(
                "session_team_live_session_required",
                "Session Team must belong to a LiveSession.",
                UmbralFailureCategory.Validation);
        }

        return new SessionTeam(
            id == Guid.Empty ? Guid.NewGuid() : id,
            liveSessionId,
            NormalizeTeamDisplayName(name),
            createdAtUtc);
    }

    public static string NormalizeTeamName(string? name)
        => NormalizeTeamDisplayName(name).ToUpperInvariant();

    private static string NormalizeTeamDisplayName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new UmbralDomainException(
                "session_team_name_required",
                "Session Team name is required.",
                UmbralFailureCategory.Validation);
        }

        var normalized = name.Trim();
        if (normalized.Length > NameMaximumLength)
        {
            throw new UmbralDomainException(
                "session_team_name_too_long",
                $"Session Team name cannot exceed {NameMaximumLength} characters.",
                UmbralFailureCategory.Validation);
        }

        return normalized;
    }
}
