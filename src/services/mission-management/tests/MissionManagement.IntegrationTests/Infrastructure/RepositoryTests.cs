using Microsoft.EntityFrameworkCore;
using MissionManagement.Domain.Missions;
using MissionManagement.Infrastructure.Persistence;
using MissionManagement.UnitTests;
using Xunit;

namespace MissionManagement.IntegrationTests.Infrastructure;

/// <summary>
/// Exercises the production <see cref="Repository{T}"/> (internal — visible here via
/// InternalsVisibleTo) against an InMemory <see cref="MissionManagementDbContext"/>. The
/// context doubles as the unit of work, so SaveChangesAsync is invoked on it directly.
/// The Mission scalar columns round-trip; its path-item tree is ignored by the EF model.
/// </summary>
public sealed class RepositoryTests
{
    [Fact]
    public async Task Add_ThenSaveChanges_RoundTripsThroughGetByIdAsync()
    {
        await using var db = CreateContext();
        var repository = new Repository<Mission>(db);
        var mission = SampleMissions.SingleTreasureHunt(Guid.NewGuid(), "Round Trip");

        repository.Add(mission);
        await db.SaveChangesAsync();

        var loaded = await repository.GetByIdAsync(mission.Id);

        Assert.NotNull(loaded);
        Assert.Equal(mission.Id, loaded!.Id);
        Assert.Equal("Round Trip", loaded.Name);
    }

    [Fact]
    public async Task Update_PersistsChangedScalar()
    {
        await using var db = CreateContext();
        var repository = new Repository<Mission>(db);
        var mission = SampleMissions.SingleTreasureHunt(Guid.NewGuid(), "Original Name");
        repository.Add(mission);
        await db.SaveChangesAsync();

        mission.UpdateDetails("Renamed Mission", "Updated description.", 45);
        repository.Update(mission);
        await db.SaveChangesAsync();

        var loaded = await repository.GetByIdAsync(mission.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Renamed Mission", loaded!.Name);
        Assert.Equal(45, loaded.MaximumDurationMinutes);
    }

    [Fact]
    public async Task Remove_DeletesTheEntity()
    {
        await using var db = CreateContext();
        var repository = new Repository<Mission>(db);
        var mission = SampleMissions.SingleTreasureHunt(Guid.NewGuid(), "Doomed");
        repository.Add(mission);
        await db.SaveChangesAsync();

        repository.Remove(mission);
        await db.SaveChangesAsync();

        var loaded = await repository.GetByIdAsync(mission.Id);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task GetAsync_WithPredicate_ReturnsOnlyMatchingRows()
    {
        await using var db = CreateContext();
        var repository = new Repository<Mission>(db);
        var shortMission = SampleMissions.SingleTreasureHunt(Guid.NewGuid(), "Short One");
        shortMission.UpdateDetails("Short One", "Short.", 10);
        var longMission = SampleMissions.SingleTrivia(Guid.NewGuid(), "Long One");
        longMission.UpdateDetails("Long One", "Long.", 120);
        repository.Add(shortMission);
        repository.Add(longMission);
        await db.SaveChangesAsync();

        var matches = await repository.GetAsync(mission => mission.MaximumDurationMinutes >= 60);

        var match = Assert.Single(matches);
        Assert.Equal(longMission.Id, match.Id);
    }

    [Fact]
    public async Task Queryable_LinqQuery_OverRepository_ReturnsExpectedRows()
    {
        await using var db = CreateContext();
        var repository = new Repository<Mission>(db);
        var target = SampleMissions.SingleTreasureHunt(Guid.NewGuid(), "Find Me");
        var other = SampleMissions.SingleTrivia(Guid.NewGuid(), "Skip Me");
        repository.Add(target);
        repository.Add(other);
        await db.SaveChangesAsync();

        var names = await repository
            .Where(mission => mission.Name == "Find Me")
            .Select(mission => mission.Name)
            .ToListAsync();

        Assert.Equal(new[] { "Find Me" }, names);
    }

    private static MissionManagementDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MissionManagementDbContext>()
            .UseInMemoryDatabase($"mission-{Guid.NewGuid():N}")
            .Options;
        return new MissionManagementDbContext(options);
    }
}
