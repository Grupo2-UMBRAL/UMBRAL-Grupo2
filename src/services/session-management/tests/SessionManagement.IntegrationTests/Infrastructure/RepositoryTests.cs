using Microsoft.EntityFrameworkCore;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Infrastructure.Persistence;
using Xunit;

namespace SessionManagement.IntegrationTests.Infrastructure;

public sealed class RepositoryTests
{
    private static readonly Guid LiveSessionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static SessionManagementDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<SessionManagementDbContext>()
            .UseInMemoryDatabase($"sess-{Guid.NewGuid():N}")
            .Options);

    private static SessionTeam NewTeam(Guid id, string name) =>
        SessionTeam.Create(LiveSessionId, id, name, DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task AddThenGetByIdAsync_RoundTripsEntity()
    {
        await using var db = CreateContext();
        var repository = new Repository<SessionTeam>(db);
        var team = NewTeam(Guid.NewGuid(), "Alpha");

        repository.Add(team);
        await db.SaveChangesAsync();

        var loaded = await repository.GetByIdAsync(team.Id);

        Assert.NotNull(loaded);
        Assert.Equal(team.Id, loaded!.Id);
        Assert.Equal("Alpha", loaded.Name);
        Assert.Equal(LiveSessionId, loaded.LiveSessionId);
    }

    [Fact]
    public async Task Update_PersistsChangedEntity()
    {
        var teamId = Guid.NewGuid();
        await using (var seedDb = CreateContextSharedDatabase(out var databaseName))
        {
            new Repository<SessionTeam>(seedDb).Add(NewTeam(teamId, "Original"));
            await seedDb.SaveChangesAsync();

            // Fresh context against the same in-memory database avoids tracking the seeded instance.
            await using var updateDb = CreateContext(databaseName);
            var updateRepository = new Repository<SessionTeam>(updateDb);
            // Same Id is preserved by SessionTeam.Create, so Update overwrites the stored row.
            updateRepository.Update(NewTeam(teamId, "Renamed"));
            await updateDb.SaveChangesAsync();

            await using var verifyDb = CreateContext(databaseName);
            var reloaded = await new Repository<SessionTeam>(verifyDb).GetByIdAsync(teamId);
            Assert.NotNull(reloaded);
            Assert.Equal("Renamed", reloaded!.Name);
        }
    }

    [Fact]
    public async Task Remove_DeletesEntity()
    {
        await using var db = CreateContext();
        var repository = new Repository<SessionTeam>(db);
        var team = NewTeam(Guid.NewGuid(), "ToDelete");
        repository.Add(team);
        await db.SaveChangesAsync();

        repository.Remove(team);
        await db.SaveChangesAsync();

        Assert.Null(await repository.GetByIdAsync(team.Id));
    }

    [Fact]
    public async Task GetAsync_FiltersByPredicate()
    {
        await using var db = CreateContext();
        var repository = new Repository<SessionTeam>(db);
        var otherLiveSessionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        repository.Add(NewTeam(Guid.NewGuid(), "Alpha"));
        repository.Add(NewTeam(Guid.NewGuid(), "Bravo"));
        repository.Add(SessionTeam.Create(otherLiveSessionId, Guid.NewGuid(), "Charlie", DateTimeOffset.UnixEpoch));
        await db.SaveChangesAsync();

        var matches = await repository.GetAsync(team => team.LiveSessionId == LiveSessionId);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, team => Assert.Equal(LiveSessionId, team.LiveSessionId));
        Assert.Contains(matches, team => team.Name == "Alpha");
        Assert.Contains(matches, team => team.Name == "Bravo");
    }

    [Fact]
    public async Task IQueryableComposition_QueriesUnderlyingSet()
    {
        await using var db = CreateContext();
        var repository = new Repository<SessionTeam>(db);
        repository.Add(NewTeam(Guid.NewGuid(), "Alpha"));
        repository.Add(NewTeam(Guid.NewGuid(), "Bravo"));
        await db.SaveChangesAsync();

        // Repository<T> is IQueryable<T>; LINQ composes onto the DbSet.
        var names = await repository
            .Where(team => team.NormalizedName == "ALPHA")
            .Select(team => team.Name)
            .ToListAsync();

        var name = Assert.Single(names);
        Assert.Equal("Alpha", name);
    }

    private static SessionManagementDbContext CreateContext(string databaseName) =>
        new(new DbContextOptionsBuilder<SessionManagementDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    private static SessionManagementDbContext CreateContextSharedDatabase(out string databaseName)
    {
        databaseName = $"sess-{Guid.NewGuid():N}";
        return CreateContext(databaseName);
    }
}
