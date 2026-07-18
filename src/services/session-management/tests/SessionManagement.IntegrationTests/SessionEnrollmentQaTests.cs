using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Features.SessionEnrollment;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Infrastructure.Persistence;
using Xunit;

namespace SessionManagement.IntegrationTests;

public sealed class SessionEnrollmentQaTests
{
    [Fact]
    public async Task GenerateJoinCodeCommand_ReturnsStableCode_WhenLiveSessionAlreadyHasOne()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateLiveSession();
        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync();
        var handler = new GenerateJoinCodeHandler(
            new LiveSessionRepository(dbContext),
            new SequenceJoinCodeGenerator("ABC234", "DEF567"));

        var first = await handler.Handle(new GenerateJoinCodeCommand(liveSession.Id), CancellationToken.None);
        var second = await handler.Handle(new GenerateJoinCodeCommand(liveSession.Id), CancellationToken.None);

        Assert.Equal("ABC234", first.JoinCode);
        Assert.Equal(first.JoinCode, second.JoinCode);
        Assert.Equal("ABC234", liveSession.JoinCodeValue);
    }

    [Fact]
    public void RegisterTeam_ThrowsBusinessException_WhenEnrollmentWindowIsClosed()
    {
        var liveSession = CreateLiveSession();
        var joinCode = JoinCode.Parse("ABC234");
        var openedAtUtc = new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero);
        liveSession.AssignJoinCode(joinCode);
        liveSession.OpenEnrollmentWindow(openedAtUtc);
        liveSession.CloseEnrollmentWindow(openedAtUtc.AddMinutes(5));

        var exception = Assert.Throws<UmbralDomainException>(() => liveSession.RegisterTeam(
            Guid.NewGuid(),
            "Team Cave",
            joinCode,
            openedAtUtc.AddMinutes(6)));

        Assert.Equal("live_session_enrollment_window_not_active", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
    }

    [Fact]
    public void RegisterTeam_CreatesSessionTeam_WhenEnrollmentWindowIsActive()
    {
        var liveSession = CreateLiveSession();
        var joinCode = JoinCode.Parse("ABC234");
        var nowUtc = new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero);
        liveSession.AssignJoinCode(joinCode);
        liveSession.OpenEnrollmentWindow(nowUtc);

        var sessionTeam = liveSession.RegisterTeam(
            Guid.NewGuid(),
            "Team Cave",
            joinCode,
            nowUtc);
        liveSession.EnrollParticipantInTeam(sessionTeam.Id, "participant-1", joinCode, nowUtc);

        Assert.Equal("Team Cave", sessionTeam.Name);
        Assert.Single(liveSession.SessionTeams);
        var participation = Assert.Single(liveSession.TeamParticipations);
        Assert.Equal(sessionTeam.Id, participation.SessionTeamId);
        Assert.Equal("participant-1", participation.ParticipantUserId);
    }

    [Fact]
    public void RegisterTeam_KeepsOneParticipation_WhenSameUserSelectsAnotherTeamInSameSession()
    {
        var liveSession = CreateLiveSession();
        var joinCode = JoinCode.Parse("ABC234");
        var nowUtc = new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero);
        liveSession.AssignJoinCode(joinCode);
        liveSession.OpenEnrollmentWindow(nowUtc);

        var firstTeam = liveSession.RegisterTeam(
            Guid.NewGuid(),
            "Team Cave",
            joinCode,
            nowUtc);
        liveSession.EnrollParticipantInTeam(firstTeam.Id, "participant-1", joinCode, nowUtc);
        var secondTeam = liveSession.RegisterTeam(
            Guid.NewGuid(),
            "Team River",
            joinCode,
            nowUtc.AddMinutes(1));
        liveSession.EnrollParticipantInTeam(secondTeam.Id, "participant-1", joinCode, nowUtc.AddMinutes(1));

        Assert.NotEqual(firstTeam.Id, secondTeam.Id);
        Assert.Equal(2, liveSession.SessionTeams.Count);
        var participation = Assert.Single(liveSession.TeamParticipations);
        Assert.Equal(secondTeam.Id, participation.SessionTeamId);
        Assert.Equal("participant-1", participation.ParticipantUserId);
    }

    private static SessionManagementDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SessionManagementDbContext>()
            .UseInMemoryDatabase($"session-enrollment-qa-{Guid.NewGuid():N}")
            .Options;

        return new SessionManagementDbContext(options);
    }

    private static LiveSession CreateLiveSession()
    {
        return LiveSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Night Mission",
            "Session Alpha",
            scheduledStartAtUtc: null,
            createdAtUtc: new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero),
            sessionStageFlow:
            [
                LiveSessionStage.Create(
                    Guid.NewGuid(),
                    "Stage 1",
                    1,
                    1,
                    30,
                    "Medium",
                    "Trivia",
                    "What symbol completes the mural?")
            ]);
    }

    private sealed class SequenceJoinCodeGenerator : IJoinCodeGenerator
    {
        private readonly Queue<string> joinCodes;

        public SequenceJoinCodeGenerator(params string[] joinCodes)
        {
            this.joinCodes = new Queue<string>(joinCodes);
        }

        public JoinCode Generate()
        {
            return JoinCode.Parse(joinCodes.Dequeue());
        }
    }
}
