using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Infrastructure.Persistence;
using Xunit;

namespace SessionManagement.UnitTests.Infrastructure.Persistence;

public class RepositoryTests
{
    private SessionManagementDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SessionManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        var dbContext = new SessionManagementDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private LiveSession CreateDummySession(Guid? id = null)
    {
        var stage = LiveSessionStage.Create(
            Guid.NewGuid(), "Stage 1", 1, 1, 10, "Easy", "Trivia", "Q1");

        return LiveSession.Create(
            id ?? Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Mission",
            "Test Session",
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow,
            new[] { stage });
    }

    [Fact]
    public async Task IQueryable_Implementation_Properties_AreCovered()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repository = new Repository<LiveSession>(dbContext);

        repository.Add(CreateDummySession());
        await dbContext.SaveChangesAsync();

        // Act & Assert
        Assert.NotNull(repository.ElementType);
        Assert.NotNull(repository.Expression);
        Assert.NotNull(repository.Provider);
        
        var enumerator = repository.GetEnumerator();
        Assert.NotNull(enumerator);
        
        var enumerable = (System.Collections.IEnumerable)repository;
        Assert.NotNull(enumerable.GetEnumerator());
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAll()
    {
        using var dbContext = CreateDbContext();
        var repository = new Repository<LiveSession>(dbContext);
        
        repository.Add(CreateDummySession());
        repository.Add(CreateDummySession());
        await dbContext.SaveChangesAsync();

        var result = await repository.GetAllAsync();
        Assert.Equal(2, result.Count);
    }
    
    [Fact]
    public async Task GetByIdAsync_ShouldReturnEntity()
    {
        using var dbContext = CreateDbContext();
        var repository = new Repository<LiveSession>(dbContext);
        var session = CreateDummySession();
        repository.Add(session);
        await dbContext.SaveChangesAsync();

        var result = await repository.GetByIdAsync(session.Id);
        Assert.NotNull(result);
        Assert.Equal(session.Id, result.Id);
    }
    
    [Fact]
    public async Task SingleOrDefaultAsync_FirstOrDefaultAsync_GetAsync()
    {
        using var dbContext = CreateDbContext();
        var repository = new Repository<LiveSession>(dbContext);
        var session = CreateDummySession();
        repository.Add(session);
        await dbContext.SaveChangesAsync();

        var singleResult = await repository.SingleOrDefaultAsync(x => x.Id == session.Id);
        Assert.NotNull(singleResult);
        
        var firstResult = await repository.FirstOrDefaultAsync(x => x.Id == session.Id);
        Assert.NotNull(firstResult);
        
        var getResult = await repository.GetAsync(x => x.Id == session.Id);
        Assert.Single(getResult);
    }

    [Fact]
    public async Task Update_Remove()
    {
        using var dbContext = CreateDbContext();
        var repository = new Repository<LiveSession>(dbContext);
        var session = CreateDummySession();
        repository.Add(session);
        await dbContext.SaveChangesAsync();

        repository.Update(session);
        await dbContext.SaveChangesAsync();

        repository.Remove(session);
        await dbContext.SaveChangesAsync();

        var result = await repository.GetByIdAsync(session.Id);
        Assert.Null(result);
    }
}
