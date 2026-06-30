using Microsoft.EntityFrameworkCore;
using ScoringMonitoring.Domain.Audit;
using ScoringMonitoring.Domain.Scoreboards;
using ScoringMonitoring.Infrastructure.Persistence;
using Xunit;

namespace ScoringMonitoring.IntegrationTests.Infrastructure;

public sealed class ApplyPenaltyScoreboardStoreTests
{
    [Fact]
    public async Task LoadAsync_ReturnsExistingScoreboardWithScoreEntriesAndRebuiltState()
    {
        var databaseName = $"infra-{Guid.NewGuid():N}";
        var liveSessionId = Guid.NewGuid();
        var sessionTeamId = Guid.NewGuid();

        await using (var seedDbContext = CreateDbContext(databaseName))
        {
            var scoreboard = new Scoreboard(liveSessionId);
            scoreboard.GrantPlayCredit(
                sessionTeamId,
                Guid.NewGuid(),
                PlayDifficulty.Medium,
                TimeSpan.FromSeconds(10),
                DateTimeOffset.Parse("2026-06-04T01:00:00Z"));
            scoreboard.GrantPlayCredit(
                sessionTeamId,
                Guid.NewGuid(),
                PlayDifficulty.Easy,
                TimeSpan.FromSeconds(20),
                DateTimeOffset.Parse("2026-06-04T01:05:00Z"));
            seedDbContext.Scoreboards.Add(scoreboard);
            await seedDbContext.SaveChangesAsync();
        }

        await using var dbContext = CreateDbContext(databaseName);
        var store = new ApplyPenaltyScoreboardStore(dbContext);

        var loaded = await store.LoadAsync(liveSessionId, CancellationToken.None);

        Assert.Equal(liveSessionId, loaded.LiveSessionId);
        Assert.Equal(2, loaded.ScoreEntries.Count);
        var teamScore = loaded.GetTeamScore(sessionTeamId);
        Assert.Equal(300, teamScore.AccumulatedScore);
        Assert.Equal(300, teamScore.VisibleScore);
    }

    [Fact]
    public async Task LoadAsync_CreatesNewScoreboardWhenNoneExistsForLiveSession()
    {
        var databaseName = $"infra-{Guid.NewGuid():N}";
        var liveSessionId = Guid.NewGuid();
        await using var dbContext = CreateDbContext(databaseName);
        var store = new ApplyPenaltyScoreboardStore(dbContext);

        var loaded = await store.LoadAsync(liveSessionId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(liveSessionId, loaded.LiveSessionId);
        Assert.Empty(loaded.ScoreEntries);
        Assert.Equal(EntityState.Added, dbContext.Entry(loaded).State);
    }

    [Fact]
    public async Task PersistPenaltyApplicationAsync_AppendsSessionEventLogAndPersistsToDatabase()
    {
        var databaseName = $"infra-{Guid.NewGuid():N}";
        var liveSessionId = Guid.NewGuid();
        var eventLog = new SessionEventLog(
            Guid.NewGuid(),
            liveSessionId,
            "PenaltyApplied",
            "Penalty applied to team.",
            DateTimeOffset.Parse("2026-06-04T02:15:00Z"));

        await using (var dbContext = CreateDbContext(databaseName))
        {
            var store = new ApplyPenaltyScoreboardStore(dbContext);
            await store.PersistPenaltyApplicationAsync(eventLog, CancellationToken.None);
        }

        await using var verifyDbContext = CreateDbContext(databaseName);
        var persisted = await verifyDbContext.SessionEventLogs.SingleAsync();
        Assert.Equal(eventLog.Id, persisted.Id);
        Assert.Equal(liveSessionId, persisted.LiveSessionId);
        Assert.Equal("PenaltyApplied", persisted.EventType);
        Assert.Equal("Penalty applied to team.", persisted.Description);
    }

    private static ScoringMonitoringDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ScoringMonitoringDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ScoringMonitoringDbContext(options);
    }
}
