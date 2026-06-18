using ScoringMonitoring.Domain.Rankings;
using ScoringMonitoring.Domain.Scoreboards;

namespace ScoringMonitoring.Application.Features.Rankings;

public static class RankingProjection
{
    public static RankingPayload Create(Scoreboard scoreboard, DateTimeOffset generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(scoreboard);

        var comparer = new RankingComparer();
        var rankingEntries = scoreboard.TeamScores
            .Select(teamScore => new RankingEntry(
                teamScore.SessionTeamId,
                teamScore.VisibleScore,
                CalculateResolutionTime(scoreboard.ScoreEntries, teamScore.SessionTeamId)))
            .ToList();

        rankingEntries.Sort(comparer);

        var items = new List<RankingItem>(rankingEntries.Count);
        RankingEntry? previousEntry = null;
        var previousRank = 0;

        for (var index = 0; index < rankingEntries.Count; index++)
        {
            var entry = rankingEntries[index];
            var rank = previousEntry is not null && comparer.Compare(previousEntry, entry) == 0
                ? previousRank
                : index + 1;

            items.Add(new RankingItem(rank, entry.SessionTeamId, entry.VisibleScore, entry.ResolutionTime));
            previousEntry = entry;
            previousRank = rank;
        }

        return new RankingPayload(scoreboard.LiveSessionId, generatedAtUtc, items);
    }

    private static TimeSpan CalculateResolutionTime(
        IReadOnlyCollection<ScoreEntry> scoreEntries,
        Guid sessionTeamId)
    {
        var totalTicks = scoreEntries
            .Where(entry =>
                entry.SessionTeamId == sessionTeamId
                && entry.ResolutionTime is not null
                && (entry.EntryType == ScoreEntryType.StageCredit
                    || entry.EntryType == ScoreEntryType.ValidationOverrideCredit))
            .Sum(entry => entry.ResolutionTime!.Value.Ticks);

        return TimeSpan.FromTicks(totalTicks);
    }
}
