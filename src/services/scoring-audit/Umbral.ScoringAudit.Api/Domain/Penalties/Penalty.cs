using Umbral.ServiceDefaults;

namespace Umbral.ScoringAudit.Api.Domain.Penalties;

public enum PenaltySeverity
{
    Minor = -50,
    Major = -100,
    Critical = -200
}

public sealed class Penalty
{
    public Penalty(
        Guid penaltyId,
        Guid commandId,
        Guid sessionTeamId,
        PenaltySeverity severity,
        string reason,
        DateTimeOffset recordedAt)
    {
        if (penaltyId == Guid.Empty)
        {
            throw new UmbralDomainException("penalty.empty_id", "Penalty id is required.");
        }

        if (commandId == Guid.Empty)
        {
            throw new UmbralDomainException("penalty.empty_command_id", "Penalty command id is required.");
        }

        if (sessionTeamId == Guid.Empty)
        {
            throw new UmbralDomainException("penalty.empty_session_team_id", "Session Team id is required.");
        }

        if (!Enum.IsDefined(typeof(PenaltySeverity), severity))
        {
            throw new UmbralDomainException("penalty.unknown_severity", "Penalty Severity is not supported.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        PenaltyId = penaltyId;
        CommandId = commandId;
        SessionTeamId = sessionTeamId;
        Severity = severity;
        Reason = reason.Trim();
        RecordedAt = recordedAt;
    }

    public Guid PenaltyId { get; }

    public Guid CommandId { get; }

    public Guid SessionTeamId { get; }

    public PenaltySeverity Severity { get; }

    public string Reason { get; }

    public DateTimeOffset RecordedAt { get; }

    public int Delta => (int)Severity;
}
