using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Features.SessionEnrollment;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Infrastructure.Persistence;
using Xunit;

namespace SessionManagement.IntegrationTests;

public sealed class SessionEnrollmentParticipantApiQaTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 6, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidateJoinCodeQuery_ReturnsNotFound_WhenJoinCodeIsNotRegistered()
    {
        await using var dbContext = CreateDbContext();
        var handler = new ValidateJoinCodeHandler(new LiveSessionReadRepository(dbContext), new FixedTimeProvider(NowUtc));

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(new ValidateJoinCodeQuery("ABC234"), CancellationToken.None));

        Assert.Equal("join_code_invalid_for_live_session", exception.Code);
        Assert.Equal(UmbralFailureCategory.NotFound, exception.Category);
    }

    [Fact]
    public async Task ValidateJoinCodeQuery_ReturnsOpenStatus_WhenEnrollmentWindowIsActive()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateLiveSessionWithJoinCode();
        liveSession.OpenEnrollmentWindow(NowUtc);
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new ValidateJoinCodeHandler(new LiveSessionReadRepository(dbContext), new FixedTimeProvider(NowUtc));

        var response = await handler.Handle(new ValidateJoinCodeQuery("ABC234"), CancellationToken.None);

        Assert.Equal(liveSession.Id, response.LiveSessionId);
        Assert.Equal("Scheduled", response.SessionState);
        Assert.Equal(NowUtc, response.OpenedAtUtc);
        Assert.Null(response.ClosedAtUtc);
        Assert.True(response.IsOpen);
    }

    [Fact]
    public async Task ValidateJoinCodeQuery_ReturnsClosedStatus_WhenEnrollmentWindowHasNotStarted()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateLiveSessionWithJoinCode();
        liveSession.OpenEnrollmentWindow(NowUtc.AddMinutes(1));
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new ValidateJoinCodeHandler(new LiveSessionReadRepository(dbContext), new FixedTimeProvider(NowUtc));

        var response = await handler.Handle(new ValidateJoinCodeQuery("ABC234"), CancellationToken.None);

        Assert.False(response.IsOpen);
        Assert.Equal(NowUtc.AddMinutes(1), response.OpenedAtUtc);
        Assert.Null(response.ClosedAtUtc);
    }

    [Fact]
    public async Task ValidateJoinCodeQuery_ReturnsClosedStatus_WhenEnrollmentWindowIsClosed()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateLiveSessionWithJoinCode();
        liveSession.OpenEnrollmentWindow(NowUtc.AddMinutes(-10));
        liveSession.CloseEnrollmentWindow(NowUtc.AddMinutes(-1));
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new ValidateJoinCodeHandler(new LiveSessionReadRepository(dbContext), new FixedTimeProvider(NowUtc));

        var response = await handler.Handle(new ValidateJoinCodeQuery("ABC234"), CancellationToken.None);

        Assert.False(response.IsOpen);
        Assert.Equal(NowUtc.AddMinutes(-10), response.OpenedAtUtc);
        Assert.Equal(NowUtc.AddMinutes(-1), response.ClosedAtUtc);
    }

    [Fact]
    public async Task ListSessionTeamsQuery_ReturnsExpectedSessionTeamsOrderedByName()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateLiveSessionWithJoinCode();
        liveSession.OpenEnrollmentWindow(NowUtc);
        var zuluTeam = liveSession.RegisterTeam(Guid.NewGuid(), "Zulu Team", JoinCode.Parse("ABC234"), NowUtc);
        liveSession.EnrollParticipantInTeam(zuluTeam.Id, "creator-zulu", JoinCode.Parse("ABC234"), NowUtc);
        var alphaTeam = liveSession.RegisterTeam(Guid.NewGuid(), "Alpha Team", JoinCode.Parse("ABC234"), NowUtc);
        liveSession.EnrollParticipantInTeam(alphaTeam.Id, "creator-alpha", JoinCode.Parse("ABC234"), NowUtc);
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new ListSessionTeamsHandler(new LiveSessionReadRepository(dbContext), new FixedTimeProvider(NowUtc));

        var response = await handler.Handle(new ListSessionTeamsQuery("ABC234"), CancellationToken.None);

        Assert.Equal(liveSession.Id, response.LiveSessionId);
        Assert.True(response.IsOpen);
        Assert.Collection(
            response.Teams,
            first =>
            {
                Assert.Equal(alphaTeam.Id, first.Id);
                Assert.Equal("Alpha Team", first.Name);
            },
            second =>
            {
                Assert.Equal(zuluTeam.Id, second.Id);
                Assert.Equal("Zulu Team", second.Name);
            });
    }

    [Fact]
    public async Task JoinSessionTeamCommand_AddsParticipantToExistingSessionTeam()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateLiveSessionWithJoinCode();
        liveSession.OpenEnrollmentWindow(NowUtc);
        var sessionTeam = liveSession.RegisterTeam(Guid.NewGuid(), "Alpha Team", JoinCode.Parse("ABC234"), NowUtc);
        liveSession.EnrollParticipantInTeam(sessionTeam.Id, "creator-alpha", JoinCode.Parse("ABC234"), NowUtc);
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new JoinSessionTeamHandler(
            new LiveSessionRepository(dbContext),
            new FixedTimeProvider(NowUtc.AddMinutes(1)),
            new StaticParticipantIdentity("participant-1"));

        var response = await handler.Handle(
            new JoinSessionTeamCommand("ABC234", sessionTeam.Id),
            CancellationToken.None);

        Assert.Equal(liveSession.Id, response.LiveSessionId);
        Assert.Equal(sessionTeam.Id, response.SessionTeamId);
        Assert.Equal("Alpha Team", response.TeamName);
        Assert.Equal("participant-1", response.ParticipantUserId);
        var participation = await dbContext.TeamParticipations.SingleAsync(
            item => item.ParticipantUserId == "participant-1");
        Assert.Equal(sessionTeam.Id, participation.SessionTeamId);
    }

    [Fact]
    public async Task JoinSessionTeamCommand_ThrowsConflict_WhenEnrollmentWindowIsClosed()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateLiveSessionWithJoinCode();
        liveSession.OpenEnrollmentWindow(NowUtc.AddMinutes(-10));
        var sessionTeam = liveSession.RegisterTeam(Guid.NewGuid(), "Alpha Team", JoinCode.Parse("ABC234"), NowUtc.AddMinutes(-9));
        liveSession.EnrollParticipantInTeam(sessionTeam.Id, "creator-alpha", JoinCode.Parse("ABC234"), NowUtc.AddMinutes(-9));
        liveSession.CloseEnrollmentWindow(NowUtc.AddMinutes(-1));
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new JoinSessionTeamHandler(
            new LiveSessionRepository(dbContext),
            new FixedTimeProvider(NowUtc),
            new StaticParticipantIdentity("participant-1"));

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(new JoinSessionTeamCommand("ABC234", sessionTeam.Id), CancellationToken.None));

        Assert.Equal("live_session_enrollment_window_not_active", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
    }

    [Fact]
    public async Task JoinSessionTeamCommand_MovesExistingParticipation_WhenParticipantSelectsAnotherTeamInsideOpenWindow()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateLiveSessionWithJoinCode();
        liveSession.OpenEnrollmentWindow(NowUtc);
        var firstTeam = liveSession.RegisterTeam(Guid.NewGuid(), "Alpha Team", JoinCode.Parse("ABC234"), NowUtc);
        liveSession.EnrollParticipantInTeam(firstTeam.Id, "creator-alpha", JoinCode.Parse("ABC234"), NowUtc);
        var secondTeam = liveSession.RegisterTeam(Guid.NewGuid(), "Beta Team", JoinCode.Parse("ABC234"), NowUtc);
        liveSession.EnrollParticipantInTeam(secondTeam.Id, "creator-beta", JoinCode.Parse("ABC234"), NowUtc);
        liveSession.EnrollParticipantInTeam(firstTeam.Id, "participant-1", JoinCode.Parse("ABC234"), NowUtc.AddMinutes(1));
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new JoinSessionTeamHandler(
            new LiveSessionRepository(dbContext),
            new FixedTimeProvider(NowUtc.AddMinutes(2)),
            new StaticParticipantIdentity("participant-1"));

        var response = await handler.Handle(
            new JoinSessionTeamCommand("ABC234", secondTeam.Id),
            CancellationToken.None);

        Assert.Equal(secondTeam.Id, response.SessionTeamId);
        var participation = await dbContext.TeamParticipations.SingleAsync(
            item => item.ParticipantUserId == "participant-1");
        Assert.Equal(secondTeam.Id, participation.SessionTeamId);
    }

    private static SessionManagementDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SessionManagementDbContext>()
            .UseInMemoryDatabase($"session-enrollment-participant-api-qa-{Guid.NewGuid():N}")
            .Options;

        return new SessionManagementDbContext(options);
    }

    private static async Task SeedLiveSessionAsync(SessionManagementDbContext dbContext, LiveSession liveSession)
    {
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync();
    }

    private static LiveSession CreateLiveSessionWithJoinCode()
    {
        var liveSession = LiveSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Night Mission",
            "Session Alpha",
            scheduledStartAtUtc: null,
            createdAtUtc: NowUtc,
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
                    "Prompt for Stage 1")
            ]);
        liveSession.AssignJoinCode(JoinCode.Parse("ABC234"));

        return liveSession;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StaticParticipantIdentity(string participantUserId) : ICurrentParticipantIdentity
    {
        public ParticipantUserId GetRequiredParticipantUserId() => ParticipantUserId.Parse(participantUserId);
    }
}
