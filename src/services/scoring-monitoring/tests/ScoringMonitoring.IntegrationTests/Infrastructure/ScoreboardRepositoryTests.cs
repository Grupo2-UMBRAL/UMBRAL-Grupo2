using Microsoft.EntityFrameworkCore;
using ScoringMonitoring.Domain.Scoreboards;
using ScoringMonitoring.Infrastructure.Persistence;
using Xunit;

namespace ScoringMonitoring.IntegrationTests.Infrastructure;

public sealed class ScoreboardRepositoryTests
{
    [Fact]
    public async Task GetByLiveSessionIdAsync_ReturnsExistingScoreboardWithScoreEntriesAndRebuiltState()
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
        var repository = new ScoreboardRepository(dbContext);

        var loaded = await repository.GetByLiveSessionIdAsync(liveSessionId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(liveSessionId, loaded.LiveSessionId);
        Assert.Equal(2, loaded.ScoreEntries.Count);
        var teamScore = loaded.GetTeamScore(sessionTeamId);
        Assert.Equal(300, teamScore.AccumulatedScore);
        Assert.Equal(300, teamScore.VisibleScore);
    }

    [Fact]
    public async Task GetRequiredByLiveSessionIdAsync_CreatesNewScoreboardWhenNoneExistsForLiveSession()
    {
        var databaseName = $"infra-{Guid.NewGuid():N}";
        var liveSessionId = Guid.NewGuid();
        await using var dbContext = CreateDbContext(databaseName);
        var repository = new ScoreboardRepository(dbContext);

        var loaded = await repository.GetRequiredByLiveSessionIdAsync(liveSessionId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(liveSessionId, loaded.LiveSessionId);
        Assert.Empty(loaded.ScoreEntries);
        Assert.Equal(EntityState.Added, dbContext.Entry(loaded).State);
    }

    private static ScoringMonitoringDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ScoringMonitoringDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ScoringMonitoringDbContext(options);
    }
}
