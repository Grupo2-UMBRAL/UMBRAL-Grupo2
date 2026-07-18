using Microsoft.EntityFrameworkCore;
using ScoringMonitoring.Domain.Audit;
using ScoringMonitoring.Infrastructure.Persistence;
using Xunit;

namespace ScoringMonitoring.IntegrationTests.Infrastructure;

public sealed class SessionEventLogRepositoryTests
{
    [Fact]
    public async Task Add_ThenSaveChanges_RoundTripsViaGetByIdAsync()
    {
        await using var dbContext = CreateDbContext();
        var repository = new SessionEventLogRepository(dbContext);
        var eventLog = CreateEventLog(eventType: "OperatorNote", description: "Nota inicial.");

        repository.Add(eventLog);
        await dbContext.SaveChangesAsync();

        var loaded = await repository.GetByIdAsync(eventLog.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(eventLog.Id, loaded!.Id);
        Assert.Equal("OperatorNote", loaded.EventType);
        Assert.Equal("Nota inicial.", loaded.Description);
    }

    [Fact]
    public async Task GetByLiveSessionIdAsync_ReturnsOnlyMatchingRows()
    {
        await using var dbContext = CreateDbContext();
        var repository = new SessionEventLogRepository(dbContext);
        var liveSessionId = Guid.NewGuid();
        var otherLiveSessionId = Guid.NewGuid();
        var matchingFirst = CreateEventLog(liveSessionId: liveSessionId, eventType: "MatchA");
        var matchingSecond = CreateEventLog(liveSessionId: liveSessionId, eventType: "MatchB");
        var nonMatching = CreateEventLog(liveSessionId: otherLiveSessionId, eventType: "Other");
        repository.Add(matchingFirst);
        repository.Add(matchingSecond);
        repository.Add(nonMatching);
        await dbContext.SaveChangesAsync();

        var matches = await repository.GetByLiveSessionIdAsync(liveSessionId, CancellationToken.None);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, entry => Assert.Equal(liveSessionId, entry.LiveSessionId));
        Assert.Contains(matches, entry => entry.Id == matchingFirst.Id);
        Assert.Contains(matches, entry => entry.Id == matchingSecond.Id);
    }

    private static SessionEventLog CreateEventLog(
        Guid? liveSessionId = null,
        string eventType = "DefaultType",
        string description = "Descripcion por defecto.")
    {
        return new SessionEventLog(
            Guid.NewGuid(),
            liveSessionId ?? Guid.NewGuid(),
            eventType,
            description,
            DateTimeOffset.Parse("2026-06-04T03:00:00Z"));
    }

    private static ScoringMonitoringDbContext CreateDbContext() =>
        CreateDbContext($"infra-{Guid.NewGuid():N}");

    private static ScoringMonitoringDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ScoringMonitoringDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ScoringMonitoringDbContext(options);
    }
}
