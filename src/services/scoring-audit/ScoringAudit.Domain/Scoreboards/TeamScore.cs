using Umbral.ServiceDefaults;

namespace ScoringAudit.Domain.Scoreboards;

public sealed class TeamScore
{
    public TeamScore(Guid sessionTeamId)
    {
        if (sessionTeamId == Guid.Empty)
        {
            throw new UmbralDomainException("team_score.empty_session_team_id", "Session Team id is required.");
        }

        SessionTeamId = sessionTeamId;
    }

    public Guid SessionTeamId { get; }

    public int AccumulatedScore { get; private set; }

    public int VisibleScore => Math.Max(0, AccumulatedScore);

    internal void Apply(int delta)
    {
        checked
        {
            AccumulatedScore += delta;
        }
    }
}
