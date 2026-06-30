using Microsoft.EntityFrameworkCore;
using ScoringMonitoring.Domain.Audit;
using ScoringMonitoring.Infrastructure.Persistence;
using Xunit;

namespace ScoringMonitoring.IntegrationTests.Infrastructure;

public sealed class RepositoryTests
{
    [Fact]
    public async Task Add_ThenSaveChanges_RoundTripsViaGetByIdAsync()
    {
        await using var dbContext = CreateDbContext();
        var repository = new Repository<SessionEventLog>(dbContext);
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
    public async Task Update_PersistsChange()
    {
        var databaseName = $"infra-{Guid.NewGuid():N}";
        var original = CreateEventLog(eventType: "OriginalType", description: "Descripcion original.");

        await using (var seedDbContext = CreateDbContext(databaseName))
        {
            new Repository<SessionEventLog>(seedDbContext).Add(original);
            await seedDbContext.SaveChangesAsync();
        }

        await using (var updateDbContext = CreateDbContext(databaseName))
        {
            var replacement = new SessionEventLog(
                original.Id,
                original.LiveSessionId,
                "UpdatedType",
                "Descripcion actualizada.",
                original.Timestamp);
            new Repository<SessionEventLog>(updateDbContext).Update(replacement);
            await updateDbContext.SaveChangesAsync();
        }

        await using var verifyDbContext = CreateDbContext(databaseName);
        var loaded = await new Repository<SessionEventLog>(verifyDbContext)
            .GetByIdAsync(original.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal("UpdatedType", loaded!.EventType);
        Assert.Equal("Descripcion actualizada.", loaded.Description);
    }

    [Fact]
    public async Task Remove_DeletesEntity()
    {
        await using var dbContext = CreateDbContext();
        var repository = new Repository<SessionEventLog>(dbContext);
        var eventLog = CreateEventLog(eventType: "ToRemove", description: "Sera eliminado.");
        repository.Add(eventLog);
        await dbContext.SaveChangesAsync();

        repository.Remove(eventLog);
        await dbContext.SaveChangesAsync();

        var loaded = await repository.GetByIdAsync(eventLog.Id, CancellationToken.None);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task GetAsync_WithPredicate_ReturnsOnlyMatchingRows()
    {
        await using var dbContext = CreateDbContext();
        var repository = new Repository<SessionEventLog>(dbContext);
        var liveSessionId = Guid.NewGuid();
        var otherLiveSessionId = Guid.NewGuid();
        var matchingFirst = CreateEventLog(liveSessionId: liveSessionId, eventType: "MatchA");
        var matchingSecond = CreateEventLog(liveSessionId: liveSessionId, eventType: "MatchB");
        var nonMatching = CreateEventLog(liveSessionId: otherLiveSessionId, eventType: "Other");
        repository.Add(matchingFirst);
        repository.Add(matchingSecond);
        repository.Add(nonMatching);
        await dbContext.SaveChangesAsync();

        var matches = await repository.GetAsync(
            entry => entry.LiveSessionId == liveSessionId,
            CancellationToken.None);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, entry => Assert.Equal(liveSessionId, entry.LiveSessionId));
        Assert.Contains(matches, entry => entry.Id == matchingFirst.Id);
        Assert.Contains(matches, entry => entry.Id == matchingSecond.Id);
    }

    [Fact]
    public async Task Repository_AsIQueryable_FiltersWithLinqQuery()
    {
        await using var dbContext = CreateDbContext();
        var repository = new Repository<SessionEventLog>(dbContext);
        var liveSessionId = Guid.NewGuid();
        var target = CreateEventLog(liveSessionId: liveSessionId, eventType: "Target");
        var other = CreateEventLog(eventType: "Other");
        repository.Add(target);
        repository.Add(other);
        await dbContext.SaveChangesAsync();

        var matches = await repository
            .Where(entry => entry.LiveSessionId == liveSessionId)
            .ToListAsync();

        Assert.Single(matches);
        Assert.Equal(target.Id, matches[0].Id);
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
