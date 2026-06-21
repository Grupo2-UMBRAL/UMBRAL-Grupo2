using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Features.SessionEnrollment;
using SessionManagement.Application.Features.SessionSnapshots;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Application.Hubs.Contracts;
using SessionManagement.Infrastructure.Persistence;
using Xunit;

namespace SessionManagement.Api.Tests;

public sealed class SessionSnapshotsQaTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 6, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid LiveSessionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid MissionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid StageOneId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid StageTwoId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StageOneHintId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid StageTwoHintId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid AlphaTeamId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid BetaTeamId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task GetLiveSessionOverviewQuery_ReturnsSessionStateAndLinearCurrentStagePerTeam()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateLiveSessionWithTeams();
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new GetLiveSessionOverviewQueryHandler(dbContext, new Repository<LiveSession>(dbContext), new FixedTimeProvider(NowUtc));

        var overview = await handler.Handle(new GetLiveSessionOverviewQuery(liveSession.Id), CancellationToken.None);

        Assert.Equal(liveSession.Id, overview.LiveSessionId);
        Assert.Equal(LiveSessionStates.Scheduled, overview.SessionState);
        Assert.Equal(600, overview.RemainingSeconds);
        Assert.Equal(SessionSnapshotConstants.InitialSequenceNumber, overview.Sync.SequenceNumber);
        Assert.Equal(NowUtc, overview.ServerTimeUtc);
        Assert.Collection(
            overview.SessionTeams,
            first =>
            {
                Assert.Equal(AlphaTeamId, first.SessionTeamId);
                Assert.Equal("Alpha Team", first.TeamName);
                Assert.Equal(1, first.ParticipantCount);
                Assert.Equal(SessionSnapshotConstants.NotStartedProgressState, first.ProgressState);
                Assert.NotNull(first.CurrentStage);
                Assert.Equal(StageOneId, first.CurrentStage.MissionStageId);
                Assert.Equal(1, first.CurrentStage.SessionStageOrder);
                Assert.Equal(10, first.CurrentStage.SourceOrder);
            },
            second =>
            {
                Assert.Equal(BetaTeamId, second.SessionTeamId);
                Assert.Equal("Beta Team", second.TeamName);
                Assert.Equal(1, second.ParticipantCount);
                Assert.NotNull(second.CurrentStage);
                Assert.Equal(StageOneId, second.CurrentStage.MissionStageId);
                Assert.Equal(1, second.CurrentStage.SessionStageOrder);
            });
    }

    [Fact]
    public async Task GetSessionTeamSnapshotQuery_ReturnsOnlyRequestedTeamDataAndSyncMetadata()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateLiveSessionWithTeams();
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new GetSessionTeamSnapshotQueryHandler(
            dbContext,
            new Repository<LiveSession>(dbContext),
            new StaticParticipantIdentity("creator-alpha"),
            new FixedTimeProvider(NowUtc));

        var snapshot = await handler.Handle(new GetSessionTeamSnapshotQuery(AlphaTeamId), CancellationToken.None);

        Assert.Equal(liveSession.Id, snapshot.LiveSessionId);
        Assert.Equal(AlphaTeamId, snapshot.SessionTeamId);
        Assert.Equal("Alpha Team", snapshot.TeamName);
        Assert.Equal(LiveSessionStates.Scheduled, snapshot.SessionState);
        Assert.Equal(SessionSnapshotConstants.NotStartedProgressState, snapshot.ProgressState);
        Assert.NotNull(snapshot.CurrentStage);
        Assert.Equal(StageOneId, snapshot.CurrentStage.MissionStageId);
        Assert.Empty(snapshot.VisibleHints);
        Assert.Equal(SessionSnapshotConstants.InitialSequenceNumber, snapshot.Sync.SequenceNumber);
        Assert.Equal(NowUtc.AddMinutes(1), snapshot.Sync.LastUpdatedUtc);
        Assert.Equal(NowUtc, snapshot.Sync.ServerTimeUtc);

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("Beta Team", json, StringComparison.Ordinal);
        Assert.DoesNotContain("creator-beta", json, StringComparison.Ordinal);
        Assert.DoesNotContain("sessionTeams", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSessionTeamDetailQuery_ReturnsOnlyRequestedSessionTeamActivity()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateLiveSessionWithTeams();
        liveSession.Start(NowUtc.AddMinutes(1));
        liveSession.ReleaseHint(AlphaTeamId, StageOneHintId, NowUtc.AddMinutes(2));
        liveSession.SubmitTriviaAnswer(AlphaTeamId, "wrong answer", NowUtc.AddMinutes(3));
        liveSession.ReleaseHint(BetaTeamId, StageOneHintId, NowUtc.AddMinutes(4));
        liveSession.SubmitTriviaAnswer(BetaTeamId, "beta answer", NowUtc.AddMinutes(5));
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new GetSessionTeamDetailQueryHandler(
            dbContext,
            new Repository<LiveSession>(dbContext),
            new FixedTimeProvider(NowUtc.AddMinutes(6)));

        var detail = await handler.Handle(
            new GetSessionTeamDetailQuery(liveSession.Id, AlphaTeamId),
            CancellationToken.None);
        var json = JsonSerializer.Serialize(detail, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(AlphaTeamId, detail.SessionTeamId);
        Assert.Equal("Alpha Team", detail.TeamName);
        Assert.Equal(1, detail.ParticipantCount);
        Assert.All(detail.ReleasedHints, hint => Assert.Equal(StageOneId, hint.MissionStageId));
        Assert.All(detail.EvidenceSubmissions, submission => Assert.Equal(StageOneId, submission.MissionStageId));
        Assert.DoesNotContain("Beta Team", json, StringComparison.Ordinal);
        Assert.DoesNotContain("beta answer", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSessionTeamDetailQuery_MarksInactive_WhenLastEvidenceSubmissionExceedsThreshold()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateLiveSessionWithTeams();
        liveSession.Start(NowUtc.AddMinutes(1));
        liveSession.SubmitTriviaAnswer(AlphaTeamId, "wrong answer", NowUtc.AddMinutes(5));
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new GetSessionTeamDetailQueryHandler(
            dbContext,
            new Repository<LiveSession>(dbContext),
            new FixedTimeProvider(NowUtc.AddMinutes(20)));

        var detail = await handler.Handle(
            new GetSessionTeamDetailQuery(liveSession.Id, AlphaTeamId, InactivityThresholdMinutes: 10),
            CancellationToken.None);

        Assert.True(detail.IsInactive);
        Assert.Equal(10, detail.InactivityThresholdMinutes);
    }

    [Fact]
    public async Task GetSessionTeamDetailQuery_KeepsActive_WhenLastEvidenceSubmissionIsInsideThreshold()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateLiveSessionWithTeams();
        liveSession.Start(NowUtc.AddMinutes(1));
        liveSession.SubmitTriviaAnswer(AlphaTeamId, "wrong answer", NowUtc.AddMinutes(15));
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new GetSessionTeamDetailQueryHandler(
            dbContext,
            new Repository<LiveSession>(dbContext),
            new FixedTimeProvider(NowUtc.AddMinutes(20)));

        var detail = await handler.Handle(
            new GetSessionTeamDetailQuery(liveSession.Id, AlphaTeamId, InactivityThresholdMinutes: 10),
            CancellationToken.None);

        Assert.False(detail.IsInactive);
    }

    [Fact]
    public async Task GetSessionTeamDetailQuery_UsesLastProgressAdvanceAsCurrentStageStartedAtUtc()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateLiveSessionWithTeams();
        liveSession.Start(NowUtc.AddMinutes(1));
        liveSession.SubmitTriviaAnswer(AlphaTeamId, "seal", NowUtc.AddMinutes(5));
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new GetSessionTeamDetailQueryHandler(
            dbContext,
            new Repository<LiveSession>(dbContext),
            new FixedTimeProvider(NowUtc.AddMinutes(8)));

        var detail = await handler.Handle(
            new GetSessionTeamDetailQuery(liveSession.Id, AlphaTeamId),
            CancellationToken.None);

        Assert.Equal(NowUtc.AddMinutes(5), detail.CurrentStageStartedAtUtc);
        Assert.NotNull(detail.CurrentStage);
        Assert.Equal(StageTwoId, detail.CurrentStage.MissionStageId);
        Assert.Equal(2, detail.CurrentStage.SessionStageOrder);
    }

    [Fact]
    public async Task GetSessionTeamDetailQuery_ReturnsHistoricalHintsAndEvidenceForEvaluatedTeam()
    {
        await using var dbContext = CreateDbContext();
        var liveSession = CreateLiveSessionWithTeams();
        liveSession.Start(NowUtc.AddMinutes(1));
        liveSession.ReleaseHint(AlphaTeamId, StageOneHintId, NowUtc.AddMinutes(2));
        var rejectedTriviaSubmission = liveSession.SubmitTriviaAnswer(AlphaTeamId, "wrong answer", NowUtc.AddMinutes(3));
        var acceptedTriviaSubmission = liveSession.SubmitTriviaAnswer(AlphaTeamId, "seal", NowUtc.AddMinutes(5));
        liveSession.ReleaseHint(AlphaTeamId, StageTwoHintId, NowUtc.AddMinutes(6));
        var rejectedQrSubmission = liveSession.SubmitEvidence(AlphaTeamId, "wrong-qr", NowUtc.AddMinutes(8));
        await SeedLiveSessionAsync(dbContext, liveSession);
        var handler = new GetSessionTeamDetailQueryHandler(
            dbContext,
            new Repository<LiveSession>(dbContext),
            new FixedTimeProvider(NowUtc.AddMinutes(20)));

        var detail = await handler.Handle(
            new GetSessionTeamDetailQuery(liveSession.Id, AlphaTeamId, InactivityThresholdMinutes: 10),
            CancellationToken.None);

        Assert.Equal(liveSession.Id, detail.LiveSessionId);
        Assert.Equal(AlphaTeamId, detail.SessionTeamId);
        Assert.Equal("Alpha Team", detail.TeamName);
        Assert.Equal(1, detail.ParticipantCount);
        Assert.Equal(SessionTeamProgressStates.InProgress, detail.ProgressState);
        Assert.True(detail.IsInactive);
        Assert.Equal(10, detail.InactivityThresholdMinutes);
        Assert.Equal(NowUtc.AddMinutes(5), detail.CurrentStageStartedAtUtc);
        Assert.NotNull(detail.CurrentStage);
        Assert.Equal(StageTwoId, detail.CurrentStage.MissionStageId);

        Assert.Collection(
            detail.ReleasedHints,
            first =>
            {
                Assert.Equal(StageTwoHintId, first.HintId);
                Assert.Equal(StageTwoId, first.MissionStageId);
                Assert.Equal("Follow the blue marker.", first.Content);
                Assert.Equal(NowUtc.AddMinutes(6), first.ReleasedAtUtc);
            },
            second =>
            {
                Assert.Equal(StageOneHintId, second.HintId);
                Assert.Equal(StageOneId, second.MissionStageId);
                Assert.Equal("Look under the seal.", second.Content);
                Assert.Equal(NowUtc.AddMinutes(2), second.ReleasedAtUtc);
            });

        Assert.Collection(
            detail.EvidenceSubmissions,
            first =>
            {
                Assert.Equal(rejectedQrSubmission.Id, first.Id);
                Assert.Equal(StageTwoId, first.MissionStageId);
                Assert.Equal("Stage Two", first.StageName);
                Assert.Equal(2, first.SessionStageOrder);
                Assert.Equal("Hard", first.Difficulty);
                Assert.Equal("Treasure Hunt", first.GameType);
                Assert.Equal("wrong-qr", first.SubmittedHash);
                Assert.Equal(ValidationOutcome.Rejected.ToString(), first.ValidationOutcome);
                Assert.False(first.IsTriviaCorrectionEligible);
            },
            second =>
            {
                Assert.Equal(acceptedTriviaSubmission.Id, second.Id);
                Assert.Equal(StageOneId, second.MissionStageId);
                Assert.Equal("Trivia", second.GameType);
                Assert.Equal("seal", second.SubmittedText);
                Assert.Equal(ValidationOutcome.Accepted.ToString(), second.ValidationOutcome);
                Assert.False(second.IsTriviaCorrectionEligible);
            },
            third =>
            {
                Assert.Equal(rejectedTriviaSubmission.Id, third.Id);
                Assert.Equal(StageOneId, third.MissionStageId);
                Assert.Equal("Trivia", third.GameType);
                Assert.Equal("wrong answer", third.SubmittedText);
                Assert.Equal(ValidationOutcome.Rejected.ToString(), third.ValidationOutcome);
                Assert.True(third.IsTriviaCorrectionEligible);
            });
    }

    [Fact]
    public void SignalRPayloads_SerializeWithCamelCaseAndOmitNullOptionalFields()
    {
        var metadata = new RealtimeEventMetadata(
            LiveSessionId,
            7,
            NowUtc,
            SnapshotRefreshPolicy.ApplyIncremental,
            "session_state_changed");
        var stateChanged = new SessionStateChangedPayload(
            metadata,
            "Scheduled",
            "Active",
            RemainingSeconds: null);
        var progressChanged = new TeamProgressChangedPayload(
            metadata,
            AlphaTeamId,
            PreviousStage: null,
            new CurrentSessionStageSnapshot(StageTwoId, "Stage Two", 2, 20, 45, "Hard", "Qr", "Stage Two prompt"),
            "InProgress");
        var hintUnlocked = new HintUnlockedPayload(
            metadata,
            AlphaTeamId,
            new VisibleHintSnapshot(
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                StageOneId,
                "Look under the seal.",
                IsSolution: false,
                Latitude: null,
                Longitude: null,
                NowUtc,
                "manual"));
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var stateJson = JsonSerializer.Serialize(stateChanged, options);
        var progressJson = JsonSerializer.Serialize(progressChanged, options);
        var hintJson = JsonSerializer.Serialize(hintUnlocked, options);

        Assert.Contains("\"currentState\":\"Active\"", stateJson, StringComparison.Ordinal);
        Assert.Contains("\"refreshPolicy\":1", stateJson, StringComparison.Ordinal);
        Assert.DoesNotContain("remainingSeconds", stateJson, StringComparison.Ordinal);
        Assert.Contains("\"currentStage\"", progressJson, StringComparison.Ordinal);
        Assert.DoesNotContain("previousStage", progressJson, StringComparison.Ordinal);
        Assert.Contains("\"hint\"", hintJson, StringComparison.Ordinal);
        Assert.DoesNotContain("latitude", hintJson, StringComparison.Ordinal);
        Assert.DoesNotContain("longitude", hintJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SnapshotApi_RequiresExpectedRolesForDashboardAndMobileRoutes()
    {
        await using var factory = new SessionManagementApiFactory();
        var liveSession = CreateLiveSessionWithTeams();
        await factory.SeedLiveSessionAsync(liveSession);
        var operatorClient = factory.CreateOperatorClient();
        var administratorClient = CreateClientWithRole(factory, UmbralRoles.Administrator);
        var participantClient = factory.CreateParticipantClient("creator-alpha");
        var participantForOverview = factory.CreateParticipantClient("creator-alpha");
        var operatorForTeamSnapshot = factory.CreateOperatorClient();

        var operatorOverview = await operatorClient.GetAsync(
            $"/api/session-management/live-sessions/{liveSession.Id}/overview");
        var administratorOverview = await administratorClient.GetAsync(
            $"/api/session-management/live-sessions/{liveSession.Id}/overview");
        var participantOverview = await participantForOverview.GetAsync(
            $"/api/session-management/live-sessions/{liveSession.Id}/overview");
        var participantSnapshot = await participantClient.GetAsync(
            $"/api/session-management/session-teams/{AlphaTeamId}/snapshot");
        var operatorSnapshot = await operatorForTeamSnapshot.GetAsync(
            $"/api/session-management/session-teams/{AlphaTeamId}/snapshot");

        Assert.Equal(HttpStatusCode.OK, operatorOverview.StatusCode);
        Assert.Equal(HttpStatusCode.OK, administratorOverview.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, participantOverview.StatusCode);
        Assert.Equal(HttpStatusCode.OK, participantSnapshot.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, operatorSnapshot.StatusCode);

        var overview = await operatorOverview.Content.ReadFromJsonAsync<LiveSessionOverview>();
        var snapshot = await participantSnapshot.Content.ReadFromJsonAsync<SessionTeamSnapshot>();
        Assert.NotNull(overview);
        Assert.NotNull(snapshot);
        Assert.Equal(liveSession.Id, overview.LiveSessionId);
        Assert.Equal(AlphaTeamId, snapshot.SessionTeamId);
    }

    private static HttpClient CreateClientWithRole(
        SessionManagementApiFactory factory,
        string role,
        string userId = "administrator-1")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId);

        return client;
    }

    private static SessionManagementDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SessionManagementDbContext>()
            .UseInMemoryDatabase($"session-snapshots-qa-{Guid.NewGuid():N}")
            .Options;

        return new SessionManagementDbContext(options);
    }

    private static async Task SeedLiveSessionAsync(SessionManagementDbContext dbContext, LiveSession liveSession)
    {
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync();
    }

    private static LiveSession CreateLiveSessionWithTeams()
    {
        var liveSession = LiveSession.Create(
            LiveSessionId,
            MissionId,
            "Night Mission",
            "Wave A",
            NowUtc.AddMinutes(10),
            NowUtc,
            [
                LiveSessionStage.Create(
                    StageTwoId,
                    "Stage Two",
                    2,
                    20,
                    45,
                    "Hard",
                    "Treasure Hunt",
                    "Prompt for Stage Two",
                    expectedQrHash: "stage-two-qr",
                    hints:
                    [
                        LiveSessionStageHint.Create(
                            StageTwoHintId,
                            "Follow the blue marker.",
                            isSolution: false)
                    ]),
                LiveSessionStage.Create(
                    StageOneId,
                    "Stage One",
                    1,
                    10,
                    30,
                    "Easy",
                    "Trivia",
                    "Prompt for Stage One",
                    triviaValidAnswer: "seal",
                    hints:
                    [
                        LiveSessionStageHint.Create(
                            StageOneHintId,
                            "Look under the seal.",
                            isSolution: false)
                    ])
            ]);
        liveSession.AssignJoinCode(JoinCode.Parse("ABC234"));
        liveSession.OpenEnrollmentWindow(NowUtc);
        liveSession.RegisterTeam(AlphaTeamId, "Alpha Team", "creator-alpha", JoinCode.Parse("ABC234"), NowUtc.AddMinutes(1));
        liveSession.RegisterTeam(BetaTeamId, "Beta Team", "creator-beta", JoinCode.Parse("ABC234"), NowUtc.AddMinutes(2));

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
