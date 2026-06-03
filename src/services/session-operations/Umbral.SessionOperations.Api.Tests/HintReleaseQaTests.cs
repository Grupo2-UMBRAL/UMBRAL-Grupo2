using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Application.Hints;
using Umbral.SessionOperations.Api.Application.SessionEnrollment;
using Umbral.SessionOperations.Api.Application.SessionSnapshots;
using Umbral.SessionOperations.Api.Domain.LiveSessions;
using Umbral.SessionOperations.Api.Hubs;
using Umbral.SessionOperations.Api.Hubs.Contracts;
using Umbral.SessionOperations.Api.Infrastructure;
using Xunit;

namespace Umbral.SessionOperations.Api.Tests;

public sealed class HintReleaseQaTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 6, 3, 18, 0, 0, TimeSpan.Zero);
    private static readonly Guid LiveSessionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid MissionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid StageOneId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid StageTwoId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid HintOneId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid HintTwoId = Guid.Parse("66666666-6666-6666-6666-666666666666");
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
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new CreateOperationalHintHandler(dbContext, new FixedTimeProvider(NowUtc));

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
        Assert.Equal(2, persistedStage.Hints.Count);
        Assert.Single(templateStage.Hints);
    }

    [Fact]
    public async Task GetSessionTeamSnapshot_ReturnsReleasedVisibleHintsWithUnlockMetadata()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateRunningLiveSessionWithTeams();
        liveSession.ReleaseHint(AlphaTeamId, HintOneId, NowUtc);
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new GetSessionTeamSnapshotQueryHandler(
            dbContext,
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
        var hubContext = new RecordingHubContext();
        var handler = new ReleaseHintHandler(dbContext, new FixedTimeProvider(NowUtc), hubContext);

        var visibleHints = await handler.Handle(
            new ReleaseHintCommand(LiveSessionId, SessionTeamId: null, HintOneId),
            CancellationToken.None);

        Assert.Equal(2, visibleHints.Count);
        Assert.Equal(2, dbContext.ReleasedHints.Count());
        Assert.Collection(
            hubContext.Client.HintUnlockedPayloads.OrderBy(payload => payload.SessionTeamId).ToArray(),
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
    }

    private static SessionOperationsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SessionOperationsDbContext>()
            .UseInMemoryDatabase($"hint-release-qa-{Guid.NewGuid():N}")
            .Options;

        return new SessionOperationsDbContext(options);
    }

    private static async Task SeedLiveSessionAsync(SessionOperationsDbContext dbContext, LiveSession liveSession)
    {
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync();
    }

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
                    triviaValidAnswer: "seal",
                    hints:
                    [
                        LiveSessionStageHint.Create(
                            HintOneId,
                            "Look for the blue sigil.",
                            isSolution: false)
                    ]),
                LiveSessionStage.Create(
                    StageTwoId,
                    "Stage Two",
                    2,
                    20,
                    45,
                    "Hard",
                    "TreasureHunt",
                    expectedQrHash: "qr-stage-2",
                    hints:
                    [
                        LiveSessionStageHint.Create(
                            HintTwoId,
                            "The archive mark is near the gate.",
                            isSolution: false)
                    ])
            ]);
        liveSession.AssignJoinCode(JoinCode.Parse("ABC234"));
        liveSession.OpenEnrollmentWindow(NowUtc.AddMinutes(-20));
        liveSession.RegisterTeam(AlphaTeamId, "Alpha Team", "creator-alpha", JoinCode.Parse("ABC234"), NowUtc.AddMinutes(-19));
        liveSession.RegisterTeam(BetaTeamId, "Beta Team", "creator-beta", JoinCode.Parse("ABC234"), NowUtc.AddMinutes(-18));
        ForceState(liveSession, LiveSessionStates.Running);

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

    private sealed class RecordingHubContext : IHubContext<SessionOperationsHub, ISessionClient>
    {
        public RecordingHubContext()
        {
            Client = new RecordingSessionClient();
            Clients = new RecordingHubClients(Client);
            Groups = new NoopGroupManager();
        }

        public RecordingSessionClient Client { get; }

        public IHubClients<ISessionClient> Clients { get; }

        public IGroupManager Groups { get; }
    }

    private sealed class RecordingHubClients(ISessionClient client) : IHubClients<ISessionClient>
    {
        public ISessionClient All => client;

        public ISessionClient AllExcept(IReadOnlyList<string> excludedConnectionIds) => client;

        public ISessionClient Client(string connectionId) => client;

        public ISessionClient Clients(IReadOnlyList<string> connectionIds) => client;

        public ISessionClient Group(string groupName) => client;

        public ISessionClient GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => client;

        public ISessionClient Groups(IReadOnlyList<string> groupNames) => client;

        public ISessionClient User(string userId) => client;

        public ISessionClient Users(IReadOnlyList<string> userIds) => client;
    }

    private sealed class NoopGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingSessionClient : ISessionClient
    {
        public List<HintUnlockedPayload> HintUnlockedPayloads { get; } = [];

        public Task ReceiveSessionStateChanged(SessionStateChangedPayload payload) => Task.CompletedTask;

        public Task ReceiveTeamProgressChanged(TeamProgressChangedPayload payload) => Task.CompletedTask;

        public Task ReceiveEvidenceSubmissionOutcomeChanged(EvidenceSubmissionOutcomeChangedPayload payload) => Task.CompletedTask;

        public Task ReceiveHintUnlocked(HintUnlockedPayload payload)
        {
            HintUnlockedPayloads.Add(payload);
            return Task.CompletedTask;
        }
    }
}
