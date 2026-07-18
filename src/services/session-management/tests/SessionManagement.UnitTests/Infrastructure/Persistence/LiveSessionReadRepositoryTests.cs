using Microsoft.EntityFrameworkCore;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Infrastructure.Persistence;
using Xunit;

namespace SessionManagement.UnitTests.Infrastructure.Persistence;

public sealed class LiveSessionReadRepositoryTests
{
    [Fact]
    public async Task GetByIdWithSessionTeamsAsync_ReturnsTheLiveSession()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateLiveSession();
        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync();

        var result = await new LiveSessionReadRepository(dbContext)
            .GetByIdWithSessionTeamsAsync(liveSession.Id);

        Assert.NotNull(result);
        Assert.Equal(liveSession.Id, result.Id);
    }

    [Fact]
    public async Task ListWithSessionTeamsAsync_ReturnsEveryLiveSession()
    {
        await using var dbContext = CreateDbContext();
        dbContext.LiveSessions.AddRange(CreateLiveSession(), CreateLiveSession());
        await dbContext.SaveChangesAsync();

        var result = await new LiveSessionReadRepository(dbContext)
            .ListWithSessionTeamsAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetByJoinCodeAsync_ReturnsTheMatchingLiveSession()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateLiveSession();
        liveSession.AssignJoinCode(JoinCode.Parse("ABC234"));
        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync();

        var result = await new LiveSessionReadRepository(dbContext)
            .GetByJoinCodeAsync("ABC234");

        Assert.NotNull(result);
        Assert.Equal(liveSession.Id, result.Id);
    }

    private static SessionManagementDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SessionManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SessionManagementDbContext(options);
    }

    private static LiveSession CreateLiveSession()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        return LiveSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Mission",
            "Test LiveSession",
            nowUtc.AddDays(1),
            nowUtc,
            [LiveSessionStage.Create(Guid.NewGuid(), "Play", 1, 1, 10, "Easy", "Trivia", "Question")]);
    }
}
