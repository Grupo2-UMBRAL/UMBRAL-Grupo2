using Umbral.ServiceDefaults;

namespace Umbral.SessionOperations.Api.Domain.LiveSessions;

public sealed class TeamParticipation
{
    private TeamParticipation()
    {
    }

    private TeamParticipation(
        Guid id,
        Guid liveSessionId,
        Guid sessionTeamId,
        ParticipantUserId participantUserId,
        DateTimeOffset enrolledAtUtc)
    {
        Id = id;
        LiveSessionId = liveSessionId;
        SessionTeamId = sessionTeamId;
        ParticipantUserId = participantUserId.Value;
        EnrolledAtUtc = enrolledAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public Guid SessionTeamId { get; private set; }

    public string ParticipantUserId { get; private set; } = string.Empty;

    public DateTimeOffset EnrolledAtUtc { get; private set; }

    public static TeamParticipation Create(
        Guid liveSessionId,
        Guid sessionTeamId,
        ParticipantUserId participantUserId,
        DateTimeOffset enrolledAtUtc)
    {
        if (liveSessionId == Guid.Empty)
        {
            throw new UmbralDomainException(
                "team_participation_live_session_required",
                "Team participation must belong to a LiveSession.",
                UmbralFailureCategory.Validation);
        }

        if (sessionTeamId == Guid.Empty)
        {
            throw new UmbralDomainException(
                "team_participation_team_required",
                "Team participation must reference a Session Team.",
                UmbralFailureCategory.Validation);
        }

        return new TeamParticipation(Guid.NewGuid(), liveSessionId, sessionTeamId, participantUserId, enrolledAtUtc);
    }

    public void MoveToTeam(Guid sessionTeamId, DateTimeOffset enrolledAtUtc)
    {
        if (sessionTeamId == Guid.Empty)
        {
            throw new UmbralDomainException(
                "team_participation_team_required",
                "Team participation must reference a Session Team.",
                UmbralFailureCategory.Validation);
        }

        SessionTeamId = sessionTeamId;
        EnrolledAtUtc = enrolledAtUtc;
    }
}
