using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Features.Hints;
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

public sealed class HintReleaseQaTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 6, 3, 18, 0, 0, TimeSpan.Zero);
    private static readonly Guid LiveSessionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid MissionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid StageOneId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid StageTwoId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid HintOneId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid HintTwoId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid SolutionOneId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid SolutionTwoId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid AlphaTeamId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid BetaTeamId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void ReleaseHint_AllowsOperatorToReleaseHintToEligibleTeam()
    {
        var liveSession = CreateRunningLiveSessionWithTeams();

        var releasedHint = liveSession.ReleaseHint(AlphaTeamId, HintOneId, NowUtc);

        Assert.Equal(AlphaTeamId, releasedHint.SessionTeamId);
        Assert.Equal(StageOneId, releasedHint.MissionStageId);
        Assert.Equal(HintOneId, releasedHint.HintId);
        Assert.Equal("Manual", releasedHint.UnlockReason);
        Assert.Equal(NowUtc, releasedHint.ReleasedAtUtc);
        Assert.Single(liveSession.ReleasedHints);
        Assert.Equal(1, liveSession.SequenceNumber);
    }

    [Fact]
    public void ReleaseHint_Throws_WhenHintWasAlreadyReleasedToSameTeamAndStage()
    {
        var liveSession = CreateRunningLiveSessionWithTeams();
        liveSession.ReleaseHint(AlphaTeamId, HintOneId, NowUtc);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.ReleaseHint(AlphaTeamId, HintOneId, NowUtc.AddMinutes(1)));

        Assert.Equal("released_hint_duplicate", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
        Assert.Single(liveSession.ReleasedHints);
    }

    [Fact]
    public async Task CreateOperationalHint_AddsHintToLiveSessionFlowJsonWithoutChangingTemplateStage()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateRunningLiveSessionWithTeams();
        var templateStage = liveSession.SessionStageFlow.Single(stage => stage.MissionStageId == StageOneId);
        var templateHintCount = templateStage.Hints.Count;
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new CreateOperationalHintHandler(dbContext, new Repository<LiveSession>(dbContext), new FixedTimeProvider(NowUtc));

        var response = await handler.Handle(
            new CreateOperationalHintCommand(
                LiveSessionId,
                StageOneId,
                "Clave operativa creada en vivo.",
                Latitude: null,
                Longitude: null),
            CancellationToken.None);

        var persisted = await dbContext.LiveSessions.SingleAsync(session => session.Id == LiveSessionId);
        var persistedStage = persisted.SessionStageFlow.Single(stage => stage.MissionStageId == StageOneId);
        Assert.Equal("Clave operativa creada en vivo.", response.Content);
        Assert.Contains("Clave operativa creada en vivo.", persisted.SessionStageFlowJson, StringComparison.Ordinal);
        Assert.Equal(templateHintCount + 1, persistedStage.Hints.Count);
        Assert.Equal(templateHintCount, templateStage.Hints.Count);
    }

    [Fact]
    public async Task GetSessionTeamSnapshot_ReturnsReleasedVisibleHintsWithUnlockMetadata()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateRunningLiveSessionWithTeams();
        liveSession.ReleaseHint(AlphaTeamId, HintOneId, NowUtc);
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new GetSessionTeamSnapshotQueryHandler(
            new Repository<LiveSession>(dbContext),
            new StaticParticipantIdentity("creator-alpha"),
            new FixedTimeProvider(NowUtc.AddMinutes(1)));

        var snapshot = await handler.Handle(new GetSessionTeamSnapshotQuery(AlphaTeamId), CancellationToken.None);

        var hint = Assert.Single(snapshot.VisibleHints);
        Assert.Equal(HintOneId, hint.HintId);
        Assert.Equal(StageOneId, hint.MissionStageId);
        Assert.Equal("Look for the blue sigil.", hint.Content);
        Assert.False(hint.IsSolution);
        Assert.Equal(NowUtc, hint.UnlockedAtUtc);
        Assert.Equal("Manual", hint.UnlockReason);
        Assert.Equal(1, snapshot.Sync.SequenceNumber);
        Assert.Equal(NowUtc, snapshot.Sync.LastUpdatedUtc);
    }

    [Fact]
    public async Task ReleaseHintCommand_GlobalReleaseAppliesHintToEligibleTeamsAndNotifiesThem()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateRunningLiveSessionWithTeams();
        await SeedLiveSessionAsync(dbContext, liveSession);
        var realtimeNotifier = new RecordingSessionRealtimeNotifier();
        var scoringAuditClient = new RecordingScoringMonitoringClient();
        var handler = new ReleaseHintHandler(
            dbContext,
            new Repository<LiveSession>(dbContext),
            new FixedTimeProvider(NowUtc),
            realtimeNotifier,
            scoringAuditClient);

        var visibleHints = await handler.Handle(
            new ReleaseHintCommand(LiveSessionId, SessionTeamId: null, HintOneId),
            CancellationToken.None);

        Assert.Equal(2, visibleHints.Count);
        Assert.Equal(2, dbContext.ReleasedHints.Count());
        Assert.Collection(
            realtimeNotifier.HintUnlockedPayloads.OrderBy(payload => payload.SessionTeamId).ToArray(),
            first =>
            {
                Assert.Equal(AlphaTeamId, first.SessionTeamId);
                Assert.Equal(HintOneId, first.Hint.HintId);
                Assert.Equal(SnapshotRefreshPolicy.ApplyIncremental, first.Metadata.RefreshPolicy);
            },
            second =>
            {
                Assert.Equal(BetaTeamId, second.SessionTeamId);
                Assert.Equal(HintOneId, second.Hint.HintId);
                Assert.Equal(SnapshotRefreshPolicy.ApplyIncremental, second.Metadata.RefreshPolicy);
            });
        Assert.Collection(
            scoringAuditClient.SessionEvents.OrderBy(sessionEvent => sessionEvent.SessionTeamId).ToArray(),
            first =>
            {
                Assert.Equal(LiveSessionId, first.LiveSessionId);
                Assert.Equal("HintReleased", first.EventType);
                Assert.Equal(AlphaTeamId, first.SessionTeamId);
                Assert.Contains(HintOneId.ToString(), first.Description, StringComparison.Ordinal);
                Assert.Contains(StageOneId.ToString(), first.Description, StringComparison.Ordinal);
            },
            second =>
            {
                Assert.Equal(LiveSessionId, second.LiveSessionId);
                Assert.Equal("HintReleased", second.EventType);
                Assert.Equal(BetaTeamId, second.SessionTeamId);
                Assert.Contains(HintOneId.ToString(), second.Description, StringComparison.Ordinal);
                Assert.Contains(StageOneId.ToString(), second.Description, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void FinalizeAndRevealAllHints_FinalizesSessionAndReleasesEveryHintAndSolutionForEveryTeam()
    {
        var liveSession = CreateRunningLiveSessionWithTeams();

        var releasedHints = liveSession.FinalizeAndRevealAllHints(NowUtc.AddMinutes(5));

        Assert.Equal(LiveSessionStates.Finalized, liveSession.State);
        Assert.Equal(8, releasedHints.Count);
        Assert.Equal(8, liveSession.ReleasedHints.Count);
        Assert.Equal(1, liveSession.SequenceNumber);
        Assert.All(releasedHints, releasedHint =>
        {
            Assert.Equal(LiveSessionId, releasedHint.LiveSessionId);
            Assert.Equal("Rule", releasedHint.UnlockReason);
            Assert.Equal(NowUtc.AddMinutes(5), releasedHint.ReleasedAtUtc);
        });
        AssertReleased(liveSession, AlphaTeamId, StageOneId, HintOneId);
        AssertReleased(liveSession, AlphaTeamId, StageOneId, SolutionOneId);
        AssertReleased(liveSession, AlphaTeamId, StageTwoId, HintTwoId);
        AssertReleased(liveSession, AlphaTeamId, StageTwoId, SolutionTwoId);
        AssertReleased(liveSession, BetaTeamId, StageOneId, HintOneId);
        AssertReleased(liveSession, BetaTeamId, StageOneId, SolutionOneId);
        AssertReleased(liveSession, BetaTeamId, StageTwoId, HintTwoId);
        AssertReleased(liveSession, BetaTeamId, StageTwoId, SolutionTwoId);
    }

    [Fact]
    public void FinalizeAndRevealAllHints_IsIdempotentAndDoesNotDuplicateReleasedHints()
    {
        var liveSession = CreateRunningLiveSessionWithTeams();

        var firstRelease = liveSession.FinalizeAndRevealAllHints(NowUtc.AddMinutes(5));
        var sequenceAfterFirstRelease = liveSession.SequenceNumber;
        var secondRelease = liveSession.FinalizeAndRevealAllHints(NowUtc.AddMinutes(6));

        Assert.Equal(LiveSessionStates.Finalized, liveSession.State);
        Assert.Equal(8, firstRelease.Count);
        Assert.Empty(secondRelease);
        Assert.Equal(8, liveSession.ReleasedHints.Count);
        Assert.Equal(sequenceAfterFirstRelease, liveSession.SequenceNumber);
        Assert.Equal(
            8,
            liveSession.ReleasedHints
                .Select(releasedHint => (releasedHint.SessionTeamId, releasedHint.MissionStageId, releasedHint.HintId))
                .Distinct()
                .Count());
    }

    [Fact]
    public async Task GetSessionTeamSnapshot_DoesNotExposeSolutionHintsBeforeLiveSessionIsFinalized()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateRunningLiveSessionWithTeams();
        liveSession.ReleaseHint(AlphaTeamId, HintOneId, NowUtc.AddMinutes(1));
        liveSession.ReleaseHint(AlphaTeamId, SolutionOneId, NowUtc.AddMinutes(2));
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new GetSessionTeamSnapshotQueryHandler(
            new Repository<LiveSession>(dbContext),
            new StaticParticipantIdentity("creator-alpha"),
            new FixedTimeProvider(NowUtc.AddMinutes(3)));

        var snapshot = await handler.Handle(new GetSessionTeamSnapshotQuery(AlphaTeamId), CancellationToken.None);

        var visibleHint = Assert.Single(snapshot.VisibleHints);
        Assert.Equal(HintOneId, visibleHint.HintId);
        Assert.False(visibleHint.IsSolution);
        Assert.DoesNotContain(snapshot.VisibleHints, hint => hint.HintId == SolutionOneId || hint.IsSolution);
        Assert.Null(snapshot.AllStages);
    }

    [Fact]
    public async Task GetSessionTeamSnapshot_PreservesFinalSolutionCoordinatesInVisibleHints()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateRunningLiveSessionWithTeams();
        liveSession.FinalizeAndRevealAllHints(NowUtc.AddMinutes(5));
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new GetSessionTeamSnapshotQueryHandler(
            new Repository<LiveSession>(dbContext),
            new StaticParticipantIdentity("creator-alpha"),
            new FixedTimeProvider(NowUtc.AddMinutes(6)));

        var snapshot = await handler.Handle(new GetSessionTeamSnapshotQuery(AlphaTeamId), CancellationToken.None);

        var solution = Assert.Single(snapshot.VisibleHints, hint => hint.HintId == SolutionTwoId);
        Assert.True(solution.IsSolution);
        Assert.Equal(10.50001m, solution.Latitude);
        Assert.Equal(-66.90001m, solution.Longitude);
        Assert.NotNull(snapshot.AllStages);
        Assert.Equal(2, snapshot.AllStages.Count);
    }

    private static SessionManagementDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SessionManagementDbContext>()
            .UseInMemoryDatabase($"hint-release-qa-{Guid.NewGuid():N}")
            .Options;

        return new SessionManagementDbContext(options);
    }

    private static async Task SeedLiveSessionAsync(SessionManagementDbContext dbContext, LiveSession liveSession)
    {
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync();
    }

    private static void AssertReleased(
        LiveSession liveSession,
        Guid sessionTeamId,
        Guid missionStageId,
        Guid hintId)
        => Assert.Contains(liveSession.ReleasedHints, releasedHint =>
            releasedHint.SessionTeamId == sessionTeamId
            && releasedHint.MissionStageId == missionStageId
            && releasedHint.HintId == hintId);

    private static LiveSession CreateRunningLiveSessionWithTeams()
    {
        var liveSession = LiveSession.Create(
            LiveSessionId,
            MissionId,
            "Night Mission",
            "Wave A",
            scheduledStartAtUtc: null,
            createdAtUtc: NowUtc.AddMinutes(-30),
            sessionStageFlow:
            [
                LiveSessionStage.Create(
                    StageOneId,
                    "Stage One",
                    1,
                    10,
                    30,
                    "Easy",
                    "Trivia",
                    "Prompt for Stage One",
                    choices: [LiveSessionChoice.Create(Guid.Parse("99999999-9999-9999-9999-999999999991"), "Seal")],
                    correctChoiceId: Guid.Parse("99999999-9999-9999-9999-999999999991"),
                    hints:
                    [
                        LiveSessionStageHint.Create(
                            HintOneId,
                            "Look for the blue sigil.",
                            isSolution: false),
                        LiveSessionStageHint.Create(
                            SolutionOneId,
                            "The seal answer is hidden in blue.",
                            isSolution: true)
                    ]),
                LiveSessionStage.Create(
                    StageTwoId,
                    "Stage Two",
                    2,
                    20,
                    45,
                    "Hard",
                    "TreasureHunt",
                    "Prompt for Stage Two",
                    expectedQrHash: "qr-stage-2",
                    hints:
                    [
                        LiveSessionStageHint.Create(
                            HintTwoId,
                            "The archive mark is near the gate.",
                            isSolution: false),
                        LiveSessionStageHint.Create(
                            SolutionTwoId,
                            "The archive gate is at the marked coordinate.",
                            isSolution: true,
                            latitude: 10.50001m,
                            longitude: -66.90001m)
                    ])
            ]);
        liveSession.AssignJoinCode(JoinCode.Parse("ABC234"));
        liveSession.OpenEnrollmentWindow(NowUtc.AddMinutes(-20));
        var alphaTeam = liveSession.RegisterTeam(AlphaTeamId, "Alpha Team", JoinCode.Parse("ABC234"), NowUtc.AddMinutes(-19));
        liveSession.EnrollParticipantInTeam(alphaTeam.Id, "creator-alpha", JoinCode.Parse("ABC234"), NowUtc.AddMinutes(-19));
        var betaTeam = liveSession.RegisterTeam(BetaTeamId, "Beta Team", JoinCode.Parse("ABC234"), NowUtc.AddMinutes(-18));
        liveSession.EnrollParticipantInTeam(betaTeam.Id, "creator-beta", JoinCode.Parse("ABC234"), NowUtc.AddMinutes(-18));
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

    private sealed class RecordingSessionRealtimeNotifier : ISessionRealtimeNotifier
    {
        public List<HintUnlockedPayload> HintUnlockedPayloads { get; } = [];

        public Task NotifySessionStateChangedAsync(LiveSessionStateChangedEvent stateChangedEvent, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task NotifyTeamProgressChangedAsync(TeamProgressChangedPayload payload, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task NotifyEvidenceSubmissionOutcomeChangedAsync(EvidenceSubmissionOutcomeChangedPayload payload, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task NotifyHintUnlockedAsync(HintUnlockedPayload payload, CancellationToken cancellationToken)
        {
            HintUnlockedPayloads.Add(payload);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingScoringMonitoringClient : IScoringMonitoringClient
    {
        public List<RecordedSessionEvent> SessionEvents { get; } = [];

        public Task RecordStageCreditAsync(
            RecordStageCreditRequest request,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Application.Scoring.ApplyPenaltyResponse> ApplyPenaltyAsync(
            Application.Scoring.ApplyPenaltyRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new Application.Scoring.ApplyPenaltyResponse(
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
            SessionEvents.Add(new RecordedSessionEvent(
                liveSessionId,
                eventType,
                description,
                ExtractSessionTeamId(description)));

            return Task.CompletedTask;
        }

        private static Guid ExtractSessionTeamId(string description)
        {
            const string marker = "Session Team '";
            var start = description.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                return Guid.Empty;
            }

            start += marker.Length;
            var end = description.IndexOf('\'', start);
            return Guid.Parse(description[start..end]);
        }
    }

    private sealed record RecordedSessionEvent(
        Guid LiveSessionId,
        string EventType,
        string Description,
        Guid SessionTeamId);
}
