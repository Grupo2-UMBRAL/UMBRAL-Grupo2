using Microsoft.EntityFrameworkCore;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Infrastructure.Persistence;

namespace SessionManagement.UnitTests.Infrastructure.Persistence;

public class LiveSessionRepositoryTests
{
    [Fact]
    public async Task AddAsync_And_SaveChangesAsync_PersistLiveSession()
    {
        await using var dbContext = CreateDbContext();
        var repository = new LiveSessionRepository(dbContext);
        var session = CreateSession();

        await repository.AddAsync(session);
        await repository.SaveChangesAsync();

        var stored = await repository.GetAsync(session.Id);

        Assert.NotNull(stored);
        Assert.Equal(session.Id, stored.Id);
    }

    [Fact]
    public async Task GetByJoinCodeWithEnrollmentAsync_LoadsSessionTeamsAndParticipations()
    {
        await using var dbContext = CreateDbContext();
        var repository = new LiveSessionRepository(dbContext);
        var session = CreateSession();
        var now = DateTimeOffset.UtcNow;
        var joinCode = JoinCode.Parse("ABCD23");
        session.AssignJoinCode(joinCode);
        session.OpenEnrollmentWindow(now);
        var team = session.RegisterTeamByOperator(Guid.NewGuid(), "Team A", now);
        session.EnrollParticipantInTeam(team.Id, "participant-1", joinCode, now);
        await repository.AddAsync(session);
        await repository.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var stored = await repository.GetByJoinCodeWithEnrollmentAsync(joinCode.Value);

        Assert.NotNull(stored);
        Assert.Single(stored.SessionTeams);
        Assert.Single(stored.TeamParticipations);
    }

    [Fact]
    public async Task GetForHintReleaseAsync_LoadsOperationalCollections()
    {
        await using var dbContext = CreateDbContext();
        var repository = new LiveSessionRepository(dbContext);
        var session = CreateSession();
        var team = session.RegisterTeamByOperator(Guid.NewGuid(), "Team A", DateTimeOffset.UtcNow);
        session.Start(DateTimeOffset.UtcNow);
        var hint = session.AddOperationalHint(session.SessionStageFlow[0].MissionStageId, "Hint", null, null, DateTimeOffset.UtcNow);
        session.ReleaseHint(team.Id, hint.Id, DateTimeOffset.UtcNow);
        await repository.AddAsync(session);
        await repository.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var stored = await repository.GetForHintReleaseAsync(session.Id);

        Assert.NotNull(stored);
        Assert.Single(stored.SessionTeams);
        Assert.Single(stored.ReleasedHints);
    }

    private static SessionManagementDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<SessionManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static LiveSession CreateSession()
        => LiveSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Mission", "Session", DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow,
            [LiveSessionStage.Create(Guid.NewGuid(), "Play", 1, 1, 10, "Easy", "Trivia", "Prompt")]);
}
