using Umbral.ServiceDefaults;

namespace ScoringMonitoring.Domain.Rankings;

public sealed class RankingComparer : IComparer<RankingEntry>
{
    public static readonly TimeSpan ResolutionTimePrecision = TimeSpan.FromMilliseconds(500);

    public int Compare(RankingEntry? left, RankingEntry? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return 1;
        }

        if (right is null)
        {
            return -1;
        }

        var scoreComparison = right.VisibleScore.CompareTo(left.VisibleScore);
        if (scoreComparison != 0)
        {
            return scoreComparison;
        }

        return GetResolutionTimeBucket(left.ResolutionTime)
            .CompareTo(GetResolutionTimeBucket(right.ResolutionTime));
    }

    public static long GetResolutionTimeBucket(TimeSpan resolutionTime)
    {
        if (resolutionTime < TimeSpan.Zero)
        {
            throw new UmbralDomainException("ranking.negative_resolution_time", "Resolution Time cannot be negative.");
        }

        return resolutionTime.Ticks / ResolutionTimePrecision.Ticks;
    }
}

public sealed class RankingEntry
{
    public RankingEntry(Guid sessionTeamId, int visibleScore, TimeSpan resolutionTime)
    {
        if (sessionTeamId == Guid.Empty)
        {
            throw new UmbralDomainException("ranking.empty_session_team_id", "Session Team id is required.");
        }

        if (resolutionTime < TimeSpan.Zero)
        {
            throw new UmbralDomainException("ranking.negative_resolution_time", "Resolution Time cannot be negative.");
        }

        SessionTeamId = sessionTeamId;
        VisibleScore = Math.Max(0, visibleScore);
        ResolutionTime = resolutionTime;
    }

    public Guid SessionTeamId { get; }

    public int VisibleScore { get; }

    public TimeSpan ResolutionTime { get; }
}
