using System.Reflection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Application.LiveSessions;
using Umbral.SessionOperations.Api.Domain.LiveSessions;
using Umbral.SessionOperations.Api.Hubs;
using Umbral.SessionOperations.Api.Hubs.Contracts;
using Umbral.SessionOperations.Api.Infrastructure;
using Xunit;

namespace Umbral.SessionOperations.Api.Tests;

public sealed class SessionStageDeactivationQaTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 6, 3, 19, 0, 0, TimeSpan.Zero);
    private static readonly Guid LiveSessionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid MissionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid StageOneId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid StageTwoId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StageThreeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid AlphaTeamId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid BetaTeamId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public void IsStagePending_ReturnsFalse_WhenAnyTeamAlreadyCompletedStage()
    {
        var liveSession = CreateLiveSessionWithTeams(stageCount: 2);
        liveSession.SubmitEvidence(AlphaTeamId, "qr-stage-1", NowUtc);

        Assert.False(liveSession.IsStagePending(StageOneId));
        Assert.True(liveSession.IsStagePending(StageTwoId));
    }

    [Fact]
    public void DeactivateStage_RemovesPendingStage_ReordersFlowAndKeepsTeamsOnNextAvailableStage()
    {
        var liveSession = CreateLiveSessionWithTeams(stageCount: 3);
        liveSession.SubmitEvidence(AlphaTeamId, "wrong", NowUtc);
        liveSession.SubmitEvidence(BetaTeamId, "wrong", NowUtc);

        liveSession.DeactivateStage(StageOneId, NowUtc.AddMinutes(1));

        Assert.Collection(
            liveSession.SessionStageFlow,
            first =>
            {
                Assert.Equal(StageTwoId, first.MissionStageId);
                Assert.Equal(1, first.SessionStageOrder);
            },
            second =>
            {
                Assert.Equal(StageThreeId, second.MissionStageId);
                Assert.Equal(2, second.SessionStageOrder);
            });
        Assert.Equal(StageTwoId, liveSession.GetCurrentStageForTeam(AlphaTeamId)?.MissionStageId);
        Assert.Equal(StageTwoId, liveSession.GetCurrentStageForTeam(BetaTeamId)?.MissionStageId);
        Assert.Equal(3, liveSession.SequenceNumber);
    }

    [Fact]
    public void DeactivateStage_AdvancesTeamOnRemovedStageToNextAvailableStage()
    {
        var liveSession = CreateLiveSessionWithTeams(stageCount: 3);
        liveSession.SubmitEvidence(AlphaTeamId, "qr-stage-1", NowUtc);
        var progressBeforeDeactivation = liveSession.TeamProgressions.Single(progress =>
            progress.SessionTeamId == AlphaTeamId);

        liveSession.DeactivateStage(StageTwoId, NowUtc.AddMinutes(1));

        var progressAfterDeactivation = liveSession.TeamProgressions.Single(progress =>
            progress.SessionTeamId == AlphaTeamId);
        Assert.Equal(1, progressBeforeDeactivation.CurrentStageIndex);
        Assert.Equal(1, progressAfterDeactivation.CurrentStageIndex);
        Assert.Equal(SessionTeamProgressStates.InProgress, progressAfterDeactivation.State);
        Assert.Equal(StageThreeId, liveSession.GetCurrentStageForTeam(AlphaTeamId)?.MissionStageId);
        Assert.Collection(
            liveSession.SessionStageFlow,
            first =>
            {
                Assert.Equal(StageOneId, first.MissionStageId);
                Assert.Equal(1, first.SessionStageOrder);
            },
            second =>
            {
                Assert.Equal(StageThreeId, second.MissionStageId);
                Assert.Equal(2, second.SessionStageOrder);
            });
    }

    [Fact]
    public void DeactivateStage_Throws_WhenStageWasCompletedByAnyTeam()
    {
        var liveSession = CreateLiveSessionWithTeams(stageCount: 2);
        liveSession.SubmitEvidence(AlphaTeamId, "qr-stage-1", NowUtc);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.DeactivateStage(StageOneId, NowUtc.AddMinutes(1)));

        Assert.Equal("session_stage_not_pending", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
    }

    [Fact]
    public void DeactivateStage_Throws_WhenSessionHasOnlyOneActiveStage()
    {
        var liveSession = CreateLiveSessionWithTeams(stageCount: 1);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.DeactivateStage(StageOneId, NowUtc.AddMinutes(1)));

        Assert.Equal("session_stage_flow_last_pending_stage", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
        Assert.Single(liveSession.SessionStageFlow);
    }

    [Fact]
    public void DeactivateStage_Throws_WhenTargetIsOnlyPendingActiveStage()
    {
        var liveSession = CreateLiveSessionWithTeams(stageCount: 2);
        liveSession.SubmitEvidence(AlphaTeamId, "qr-stage-1", NowUtc);
        liveSession.SubmitEvidence(BetaTeamId, "qr-stage-1", NowUtc);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.DeactivateStage(StageTwoId, NowUtc.AddMinutes(1)));

        Assert.Equal("session_stage_flow_last_pending_stage", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
        Assert.Equal(2, liveSession.SessionStageFlow.Count);
    }

    [Fact]
    public void DeactivateStage_RejectsStageBehindTeamProgressionBecauseItWasAlreadyCompletedByThatTeam()
    {
        var liveSession = CreateLiveSessionWithTeams(stageCount: 3);
        liveSession.SubmitEvidence(AlphaTeamId, "qr-stage-1", NowUtc);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.DeactivateStage(StageOneId, NowUtc.AddMinutes(1)));

        var progress = liveSession.TeamProgressions.Single(existingProgress =>
            existingProgress.SessionTeamId == AlphaTeamId);
        Assert.Equal("session_stage_not_pending", exception.Code);
        Assert.Equal(1, progress.CurrentStageIndex);
        Assert.Equal(StageTwoId, liveSession.GetCurrentStageForTeam(AlphaTeamId)?.MissionStageId);
    }

    [Theory]
    [InlineData(LiveSessionStates.Finalized)]
    [InlineData(LiveSessionStates.Canceled)]
    public void DeactivateStage_Throws_WhenLiveSessionStateDoesNotAllowOperationalDeactivation(string state)
    {
        var liveSession = CreateLiveSessionWithTeams(stageCount: 2);
        ForceState(liveSession, state);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.DeactivateStage(StageOneId, NowUtc));

        Assert.Equal("live_session_stage_deactivation_not_allowed_for_state", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
    }

    [Fact]
    public async Task DeactivateStageCommand_PersistsFlowChangeAndPublishesRefreshSnapshotSignal()
    {
        await using var dbContext = CreateDbContext();
        await SeedLiveSessionAsync(dbContext, CreateLiveSessionWithTeams(stageCount: 2));
        var hubContext = new RecordingHubContext();
        var handler = new DeactivateStageHandler(dbContext, new FixedTimeProvider(NowUtc), hubContext);

        var response = await handler.Handle(
            new DeactivateStageCommand(LiveSessionId, StageOneId),
            CancellationToken.None);

        var persisted = await dbContext.LiveSessions.SingleAsync(session => session.Id == LiveSessionId);
        var payload = Assert.Single(hubContext.Client.SessionStateChangedPayloads);
        Assert.Single(response.SessionStageFlow);
        Assert.Single(persisted.SessionStageFlow);
        Assert.Equal(StageTwoId, persisted.SessionStageFlow.Single().MissionStageId);
        Assert.Equal(1, persisted.SessionStageFlow.Single().SessionStageOrder);
        Assert.Equal(SnapshotRefreshPolicy.RefreshSnapshot, payload.Metadata.RefreshPolicy);
        Assert.Equal(LiveSessionId, payload.Metadata.LiveSessionId);
        Assert.Equal(1, payload.Metadata.SequenceNumber);
        Assert.Equal(LiveSessionStates.Scheduled, payload.PreviousState);
        Assert.Equal(LiveSessionStates.Scheduled, payload.CurrentState);
    }

    private static SessionOperationsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SessionOperationsDbContext>()
            .UseInMemoryDatabase($"session-stage-deactivation-qa-{Guid.NewGuid():N}")
            .Options;

        return new SessionOperationsDbContext(options);
    }

    private static async Task SeedLiveSessionAsync(SessionOperationsDbContext dbContext, LiveSession liveSession)
    {
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync();
    }

    private static LiveSession CreateLiveSessionWithTeams(int stageCount)
    {
        var liveSession = LiveSession.Create(
            LiveSessionId,
            MissionId,
            "Night Mission",
            "Wave A",
            scheduledStartAtUtc: null,
            createdAtUtc: NowUtc.AddMinutes(-30),
            sessionStageFlow: CreateStages(stageCount));

        liveSession.SessionTeams.Add(SessionTeam.Create(liveSession.Id, AlphaTeamId, "Alpha Team", NowUtc.AddMinutes(-20)));
        liveSession.SessionTeams.Add(SessionTeam.Create(liveSession.Id, BetaTeamId, "Beta Team", NowUtc.AddMinutes(-19)));

        return liveSession;
    }

    private static IReadOnlyList<LiveSessionStage> CreateStages(int stageCount)
    {
        var stageIds = new[] { StageOneId, StageTwoId, StageThreeId };

        return Enumerable.Range(1, stageCount)
            .Select(stageOrder => LiveSessionStage.Create(
                stageIds[stageOrder - 1],
                $"Stage {stageOrder}",
                stageOrder,
                stageOrder,
                10,
                "Medium",
                "TreasureHunt",
                $"Prompt for Stage {stageOrder}",
                expectedQrHash: $"qr-stage-{stageOrder}"))
            .ToArray();
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
        public List<SessionStateChangedPayload> SessionStateChangedPayloads { get; } = [];

        public Task ReceiveSessionStateChanged(SessionStateChangedPayload payload)
        {
            SessionStateChangedPayloads.Add(payload);
            return Task.CompletedTask;
        }

        public Task ReceiveTeamProgressChanged(TeamProgressChangedPayload payload) => Task.CompletedTask;

        public Task ReceiveEvidenceSubmissionOutcomeChanged(EvidenceSubmissionOutcomeChangedPayload payload) => Task.CompletedTask;

        public Task ReceiveHintUnlocked(HintUnlockedPayload payload) => Task.CompletedTask;
    }
}
