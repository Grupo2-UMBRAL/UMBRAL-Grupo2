using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using SessionOperations.Application.Features.EvidenceSubmissions;
using SessionOperations.Application.Features.Penalties;
using SessionOperations.Application.Scoring;
using SessionOperations.Domain.LiveSessions;
using SessionOperations.Infrastructure.Persistence;
using Xunit;

namespace SessionOperations.Api.Tests;

public sealed class PenaltyApplicationQaTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 6, 4, 4, 30, 0, TimeSpan.Zero);
    private static readonly Guid LiveSessionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid MissionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid StageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TeamId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task ApplyPenalty_ForwardsOperatorIdentityAndClockTimestampToScoringAudit()
    {
        await using var dbContext = CreateDbContext();
        await SeedLiveSessionAsync(dbContext, CreateActiveLiveSession());
        var scoringAuditClient = new RecordingScoringAuditClient();
        var handler = new ApplyPenaltyHandler(
            dbContext,
            new FixedTimeProvider(NowUtc),
            new StaticOperatorIdentity("operator-7"),
            scoringAuditClient);
        var commandId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        var response = await handler.Handle(
            new ApplyPenaltyCommand(
                LiveSessionId,
                TeamId,
                commandId,
                "Critical",
                "Conducta antideportiva."),
            CancellationToken.None);

        var forwarded = Assert.Single(scoringAuditClient.Requests);
        Assert.Equal(LiveSessionId, forwarded.LiveSessionId);
        Assert.Equal(TeamId, forwarded.SessionTeamId);
        Assert.Equal(commandId, forwarded.CommandId);
        Assert.Equal("Critical", forwarded.Severity);
        Assert.Equal("operator-7", forwarded.AppliedByOperatorUserId);
        Assert.Equal("Conducta antideportiva.", forwarded.Reason);
        Assert.Equal(NowUtc, forwarded.RecordedAt);
        Assert.True(response.PenaltyApplied);
        Assert.Equal(150, response.VisibleScore);
    }

    [Fact]
    public async Task ApplyPenalty_RejectsBlankReasonBeforeCallingScoringAudit()
    {
        await using var dbContext = CreateDbContext();
        await SeedLiveSessionAsync(dbContext, CreateActiveLiveSession());
        var scoringAuditClient = new RecordingScoringAuditClient();
        var handler = new ApplyPenaltyHandler(
            dbContext,
            new FixedTimeProvider(NowUtc),
            new StaticOperatorIdentity("operator-7"),
            scoringAuditClient);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(
                new ApplyPenaltyCommand(
                    LiveSessionId,
                    TeamId,
                    Guid.NewGuid(),
                    "Minor",
                    " "),
                CancellationToken.None));

        Assert.Equal("penalty_reason_required", exception.Code);
        Assert.Empty(scoringAuditClient.Requests);
    }

    [Fact]
    public async Task ApplyPenalty_RejectsWhenLiveSessionStateDoesNotAllowOperationalPenalty()
    {
        await using var dbContext = CreateDbContext();
        await SeedLiveSessionAsync(dbContext, CreateScheduledLiveSession());
        var scoringAuditClient = new RecordingScoringAuditClient();
        var handler = new ApplyPenaltyHandler(
            dbContext,
            new FixedTimeProvider(NowUtc),
            new StaticOperatorIdentity("operator-7"),
            scoringAuditClient);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(
                new ApplyPenaltyCommand(
                    LiveSessionId,
                    TeamId,
                    Guid.NewGuid(),
                    "Major",
                    "Intento fuera de regla."),
                CancellationToken.None));

        Assert.Equal("live_session_not_accepting_penalties", exception.Code);
        Assert.Empty(scoringAuditClient.Requests);
    }

    private static SessionOperationsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SessionOperationsDbContext>()
            .UseInMemoryDatabase($"penalty-application-qa-{Guid.NewGuid():N}")
            .Options;

        return new SessionOperationsDbContext(options);
    }

    private static async Task SeedLiveSessionAsync(SessionOperationsDbContext dbContext, LiveSession liveSession)
    {
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync();
    }

    private static LiveSession CreateActiveLiveSession()
    {
        var liveSession = CreateScheduledLiveSession();
        ForceState(liveSession, LiveSessionStates.Active);
        return liveSession;
    }

    private static LiveSession CreateScheduledLiveSession()
    {
        var liveSession = LiveSession.Create(
            LiveSessionId,
            MissionId,
            "Penalty Mission",
            "Penalty Run",
            scheduledStartAtUtc: null,
            createdAtUtc: NowUtc.AddMinutes(-20),
            sessionStageFlow:
            [
                LiveSessionStage.Create(
                    StageId,
                    "Stage One",
                    1,
                    1,
                    15,
                    "Medium",
                    "Trivia",
                    "Prompt",
                    triviaValidAnswer: "seal")
            ]);

        liveSession.SessionTeams.Add(SessionTeam.Create(liveSession.Id, TeamId, "Alpha Team", NowUtc.AddMinutes(-10)));
        return liveSession;
    }

    private static void ForceState(LiveSession liveSession, string state)
    {
        var backingField = typeof(LiveSession).GetField("<State>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("LiveSession.State backing field was not found.");

        backingField.SetValue(liveSession, state);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StaticOperatorIdentity(string operatorUserId) : ICurrentOperatorIdentity
    {
        public string GetRequiredOperatorUserId() => operatorUserId;
    }

    private sealed class RecordingScoringAuditClient : IScoringAuditClient
    {
        public List<Application.Scoring.ApplyPenaltyRequest> Requests { get; } = [];

        public Task RecordStageCreditAsync(
            RecordStageCreditRequest request,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task LogSessionEventAsync(
            Guid liveSessionId,
            string eventType,
            string description,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Application.Scoring.ApplyPenaltyResponse> ApplyPenaltyAsync(
            Application.Scoring.ApplyPenaltyRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return Task.FromResult(new Application.Scoring.ApplyPenaltyResponse(
                request.LiveSessionId,
                request.SessionTeamId,
                request.CommandId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                true,
                150,
                new RankingPayload(
                    request.LiveSessionId,
                    request.RecordedAt,
                    [new RankingItem(1, request.SessionTeamId, 150, TimeSpan.FromSeconds(10))])));
        }
    }
}
