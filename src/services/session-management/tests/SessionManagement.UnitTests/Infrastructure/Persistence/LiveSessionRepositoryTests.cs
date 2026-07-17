using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Infrastructure.Persistence;
using Xunit;

namespace SessionManagement.UnitTests.Infrastructure.Persistence;

public class LiveSessionRepositoryTests
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
    public async Task Add_And_GetByIdAsync_ShouldReturnEntity()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repository = new LiveSessionRepository(dbContext);
        var session = CreateDummySession();

        // Act
        repository.Add(session);
        await dbContext.SaveChangesAsync();

        var retrieved = await repository.GetByIdAsync(session.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(session.Id, retrieved.Id);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllEntities()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repository = new LiveSessionRepository(dbContext);
        
        repository.Add(CreateDummySession());
        repository.Add(CreateDummySession());
        await dbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task SingleOrDefaultAsync_ShouldReturnMatchingEntity()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repository = new LiveSessionRepository(dbContext);
        
        var targetId = Guid.NewGuid();
        repository.Add(CreateDummySession(targetId));
        repository.Add(CreateDummySession());
        await dbContext.SaveChangesAsync();

        // Act
        var result = await repository.SingleOrDefaultAsync(x => x.Id == targetId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(targetId, result.Id);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_ShouldReturnMatchingEntity()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repository = new LiveSessionRepository(dbContext);
        
        repository.Add(CreateDummySession());
        repository.Add(CreateDummySession());
        await dbContext.SaveChangesAsync();

        // Act
        var result = await repository.FirstOrDefaultAsync(x => x.MissionName == "Test Mission");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Mission", result.MissionName);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnMatchingEntities()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repository = new LiveSessionRepository(dbContext);
        
        repository.Add(CreateDummySession());
        repository.Add(CreateDummySession());
        await dbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetAsync(x => x.MissionName == "Test Mission");

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Update_ShouldModifyEntity()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repository = new LiveSessionRepository(dbContext);
        
        var session = CreateDummySession();
        repository.Add(session);
        await dbContext.SaveChangesAsync();

        // Act
        var newStage = LiveSessionStage.Create(
            Guid.NewGuid(), "Stage 2", 1, 1, 15, "Medium", "Trivia", "Q2");
        session.ReplaceSessionStageFlow(new[] { newStage });
        
        repository.Update(session);
        await dbContext.SaveChangesAsync();

        var retrieved = await repository.GetByIdAsync(session.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Single(retrieved.SessionStageFlow);
        Assert.Equal("Stage 2", retrieved.SessionStageFlow[0].Name);
    }

    [Fact]
    public async Task Remove_ShouldDeleteEntity()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repository = new LiveSessionRepository(dbContext);
        
        var session = CreateDummySession();
        repository.Add(session);
        await dbContext.SaveChangesAsync();

        // Act
        repository.Remove(session);
        await dbContext.SaveChangesAsync();

        var retrieved = await repository.GetByIdAsync(session.Id);

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task GetBySessionTeamIdWithEvidenceSubmissionsAsync_ShouldReturnEntity()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repository = new LiveSessionRepository(dbContext);
        
        var session = CreateDummySession();
        var teamId = Guid.NewGuid();
        session.RegisterTeamByOperator(teamId, "Team A", DateTimeOffset.UtcNow);
        
        repository.Add(session);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetBySessionTeamIdWithEvidenceSubmissionsAsync(teamId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(session.Id, result.Id);
    }

    [Fact]
    public async Task GetByEvidenceSubmissionIdWithSubmissionsAndLogsAsync_ShouldReturnEntity()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repository = new LiveSessionRepository(dbContext);
        
        var session = CreateDummySession();
        var teamId = Guid.NewGuid();
        session.RegisterTeamByOperator(teamId, "Team A", DateTimeOffset.UtcNow);
        
        // Let's create an evidence submission
        var newStage = LiveSessionStage.Create(Guid.NewGuid(), "Treasure Stage", 1, 1, 10, "Easy", "TreasureHunt", "Q2", expectedQrHash: "some link");
        session.ReplaceSessionStageFlow(new[] { newStage });
        
        session.Start(DateTimeOffset.UtcNow);
        var submissionId = Guid.NewGuid();
        
        var submission = session.SubmitEvidence(teamId, "some link", DateTimeOffset.UtcNow);
        // We can't set the ID manually if it's created internally. Wait, SubmitEvidence returns EvidenceSubmission.
        // We can just get the ID from the returned submission!
        
        repository.Add(session);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await repository.GetByEvidenceSubmissionIdWithSubmissionsAndLogsAsync(submission.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(session.Id, result.Id);
    }

    [Fact]
    public void IQueryable_Implementation_Properties()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repository = new LiveSessionRepository(dbContext);

        // Act & Assert
        Assert.NotNull(repository.ElementType);
        Assert.NotNull(repository.Expression);
        Assert.NotNull(repository.Provider);
        Assert.NotNull(repository.GetEnumerator());
        Assert.NotNull(((System.Collections.IEnumerable)repository).GetEnumerator());
    }
}
