using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SessionManagement.Application.Features.EvidenceSubmissions;
using SessionManagement.Application.Realtime;
using SessionManagement.Application.Scoring;
using SessionManagement.Application.Features.SessionEnrollment;
using SessionManagement.Application.Features.SessionLifecycle;
using SessionManagement.Application.Features.SessionSnapshots;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Application.Hubs.Contracts;
using SessionManagement.Infrastructure.Persistence;
using Xunit;

namespace SessionManagement.IntegrationTests;

public sealed class EvidenceSubmissionAuditEventTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 6, 4, 5, 0, 0, TimeSpan.Zero);
    private static readonly Guid LiveSessionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid MissionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid StageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TeamId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task SubmitEvidenceCommand_LogsSubmittedEvidenceAndImmediateValidationOutcome()
    {
        await using var dbContext = CreateDbContext();
        await SeedLiveSessionAsync(dbContext, CreateActiveLiveSession());
        var scoringAuditClient = new RecordingScoringMonitoringClient();
        var handler = new SubmitEvidenceCommandHandler(
            dbContext,
            new Repository<LiveSession>(dbContext),
            new FixedTimeProvider(NowUtc),
            new StaticParticipantIdentity("participant-alpha"),
            new NoopSessionRealtimeNotifier(),
            scoringAuditClient);

        var response = await handler.Handle(
            new SubmitEvidenceCommand(TeamId, "wrong-qr"),
            CancellationToken.None);

        Assert.Equal("Rejected", response.ValidationOutcome);
        Assert.Collection(
            scoringAuditClient.SessionEvents,
            submitted =>
            {
                Assert.Equal(LiveSessionId, submitted.LiveSessionId);
                Assert.Equal("EvidenceSubmitted", submitted.EventType);
                Assert.Equal(
                    $"Session Team '{TeamId}' submitted evidence for Mission Stage '{StageId}'. Game Type: TreasureHunt.",
                    submitted.Description);
            },
            outcome =>
            {
                Assert.Equal(LiveSessionId, outcome.LiveSessionId);
                Assert.Equal("ValidationOutcome", outcome.EventType);
                Assert.Equal(
                    $"Evidence submission '{response.EvidenceSubmissionId}' for Session Team '{TeamId}' on Mission Stage '{StageId}' was validated as Rejected. Source: AutomaticTreasureHunt.",
                    outcome.Description);
            });
        Assert.Empty(scoringAuditClient.StageCreditRequests);
    }

    private static SessionManagementDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SessionManagementDbContext>()
            .UseInMemoryDatabase($"evidence-submission-audit-events-{Guid.NewGuid():N}")
            .Options;

        return new SessionManagementDbContext(options);
    }

    private static async Task SeedLiveSessionAsync(SessionManagementDbContext dbContext, LiveSession liveSession)
    {
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync();
    }

    private static LiveSession CreateActiveLiveSession()
    {
        var liveSession = LiveSession.Create(
            LiveSessionId,
            MissionId,
            "Evidence Mission",
            "Wave A",
            scheduledStartAtUtc: null,
            createdAtUtc: NowUtc.AddMinutes(-30),
            sessionStageFlow:
            [
                LiveSessionStage.Create(
                    StageId,
                    "QR Stage",
                    1,
                    1,
                    15,
                    "Easy",
                    "TreasureHunt",
                    "Find the seal.",
                    expectedQrHash: "expected-qr")
            ]);

        liveSession.AssignJoinCode(JoinCode.Parse("ABC234"));
        liveSession.OpenEnrollmentWindow(NowUtc.AddMinutes(-20));
        var team = liveSession.RegisterTeam(TeamId, "Alpha Team", JoinCode.Parse("ABC234"), NowUtc.AddMinutes(-10));
        liveSession.EnrollParticipantInTeam(team.Id, "participant-alpha", JoinCode.Parse("ABC234"), NowUtc.AddMinutes(-10));
        ForceState(liveSession, LiveSessionStates.Active);
        return liveSession;
    }

    private static void ForceState(LiveSession liveSession, string state)
    {
        var backingField = typeof(LiveSession).GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("LiveSession.State backing field was not found.");

        backingField.SetValue(liveSession, state);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class StaticParticipantIdentity(string participantUserId) : ICurrentParticipantIdentity
    {
        public ParticipantUserId GetRequiredParticipantUserId() => ParticipantUserId.Parse(participantUserId);
    }

    private sealed class RecordingScoringMonitoringClient : IScoringMonitoringClient
    {
        public List<RecordStageCreditRequest> StageCreditRequests { get; } = [];

        public List<RecordedSessionEvent> SessionEvents { get; } = [];

        public Task RecordStageCreditAsync(
            RecordStageCreditRequest request,
            CancellationToken cancellationToken)
        {
            StageCreditRequests.Add(request);
            return Task.CompletedTask;
        }

        public Task<ApplyPenaltyResponse> ApplyPenaltyAsync(
            ApplyPenaltyRequest request,
            CancellationToken cancellationToken)
            => Task.FromResult(new ApplyPenaltyResponse(
                request.LiveSessionId,
                request.SessionTeamId,
                request.CommandId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                true,
                0,
                new RankingPayload(request.LiveSessionId, request.RecordedAt, [])));

        public Task LogSessionEventAsync(
            Guid liveSessionId,
            string eventType,
            string description,
            CancellationToken cancellationToken)
        {
            SessionEvents.Add(new RecordedSessionEvent(liveSessionId, eventType, description));
            return Task.CompletedTask;
        }
    }

    private sealed record RecordedSessionEvent(
        Guid LiveSessionId,
        string EventType,
        string Description);

    private sealed class NoopSessionRealtimeNotifier : ISessionRealtimeNotifier
    {
        public Task NotifySessionStateChangedAsync(LiveSessionStateChangedEvent stateChangedEvent, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task NotifyTeamProgressChangedAsync(TeamProgressChangedPayload payload, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task NotifyEvidenceSubmissionOutcomeChangedAsync(EvidenceSubmissionOutcomeChangedPayload payload, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task NotifyHintUnlockedAsync(HintUnlockedPayload payload, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
