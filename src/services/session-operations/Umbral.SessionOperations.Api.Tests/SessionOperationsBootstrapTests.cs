using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Umbral.SessionOperations.Api.Application.Bootstrap.Queries;
using Umbral.SessionOperations.Api.Application.LiveSessions;
using Umbral.SessionOperations.Api.Application.SessionLifecycle;
using Umbral.SessionOperations.Api.Application.SessionEnrollment;
using Umbral.SessionOperations.Api.Domain.LiveSessions;
using Umbral.SessionOperations.Api.Infrastructure;
using Umbral.ServiceDefaults;
using Xunit;

namespace Umbral.SessionOperations.Api.Tests;

public sealed class SessionOperationsBootstrapDetailsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsConfiguredBootstrapDetails()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=umbral;Username=umbral;Password=umbral",
                ["Auth:Authority"] = "http://localhost:8080/realms/umbral",
                ["Auth:Audience"] = "umbral-session-operations-api",
                ["RabbitMQ:Host"] = "localhost",
                ["SignalR:Enabled"] = "true",
                ["Persistence:ApplyMigrationsOnStartup"] = "false"
            })
            .Build();

        var provider = new SessionOperationsBootstrapDetailsProvider(configuration);
        var handler = new GetSessionOperationsBootstrapDetailsQueryHandler(provider);

        var details = await handler.Handle(new GetSessionOperationsBootstrapDetailsQuery(), CancellationToken.None);

        Assert.True(details.DatabaseConfigured);
        Assert.Equal("localhost", details.RabbitMqHost);
        Assert.True(details.SignalREnabled);
        Assert.False(details.MigrationsApplyOnStartup);
    }
}

public sealed class SessionOperationsHealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public SessionOperationsHealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task GetHealth_ReturnsSuccess()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }
}

public sealed class SessionOperationsRoleSmokeRouteTests
{
    [Fact]
    public async Task SmokeRoute_ReturnsUnauthorized_WhenRequestHasNoIdentity()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/session-operations/smoke/operator");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SmokeRoute_ReturnsForbidden_WhenIdentityHasWrongRole()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, UmbralRoles.Participant);

        var response = await client.GetAsync("/api/session-operations/smoke/operator");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SmokeRoute_ReturnsSuccess_WhenIdentityHasRequiredRole()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, UmbralRoles.Operator);

        var response = await client.GetAsync("/api/session-operations/smoke/operator");

        response.EnsureSuccessStatusCode();
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] = "Host=localhost;Database=umbral;Username=umbral;Password=umbral",
                        ["Auth:Authority"] = "http://localhost:8080/realms/umbral",
                        ["Auth:Audience"] = "umbral-session-operations-api",
                        ["RabbitMQ:Host"] = "localhost",
                        ["SignalR:Enabled"] = "true",
                        ["Persistence:ApplyMigrationsOnStartup"] = "false"
                    });
                });

                builder.ConfigureServices(services =>
                {
                    services.AddAuthentication(options =>
                        {
                            options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                            options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                            options.DefaultScheme = TestAuthenticationHandler.SchemeName;
                        })
                        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                            TestAuthenticationHandler.SchemeName,
                            _ => { });
                });
            });

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string RoleHeaderName = "X-Test-Role";
        public const string UserIdHeaderName = "X-Test-User-Id";

        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(RoleHeaderName))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = Request.Headers[RoleHeaderName]
                .Where(static role => !string.IsNullOrWhiteSpace(role))
                .Select(role => new Claim(ClaimTypes.Role, role!))
                .ToList();
            var userId = Request.Headers.TryGetValue(UserIdHeaderName, out var userIdValues)
                ? userIdValues.FirstOrDefault()
                : "test-user";
            if (!string.IsNullOrWhiteSpace(userId))
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId!));
                claims.Add(new Claim("sub", userId!));
            }

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

public sealed class LiveSessionDomainTests
{
    [Fact]
    public void Create_RequiresSessionStageFlow()
    {
        var exception = Assert.Throws<UmbralDomainException>(() =>
            LiveSession.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Caracas Nights",
                "Friday run",
                scheduledStartAtUtc: null,
                createdAtUtc: DateTimeOffset.UtcNow,
                sessionStageFlow: Array.Empty<LiveSessionStage>()));

        Assert.Equal("live_session_stage_flow_required", exception.Code);
        Assert.Equal(UmbralFailureCategory.Validation, exception.Category);
    }

    [Fact]
    public void Create_SetsScheduledState_AndPreservesSnapshotFlow()
    {
        var liveSession = LiveSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Caracas Nights",
            "Friday run",
            scheduledStartAtUtc: new DateTimeOffset(2026, 6, 3, 20, 0, 0, TimeSpan.Zero),
            createdAtUtc: new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero),
            sessionStageFlow:
            [
                LiveSessionStage.Create(
                    Guid.NewGuid(),
                    "Stage 1",
                    1,
                    2,
                    35,
                    "Medium",
                    "Trivia",
                    "Which code opens the archive?")
            ]);

        Assert.Equal(LiveSessionStates.Scheduled, liveSession.State);
        Assert.Single(liveSession.SessionStageFlow);
        Assert.Equal("Stage 1", liveSession.SessionStageFlow[0].Name);
        Assert.Equal("Which code opens the archive?", liveSession.SessionStageFlow[0].Prompt);
    }

    [Fact]
    public void Start_RequiresAtLeastOneRegisteredSessionTeam()
    {
        var liveSession = CreateLiveSession("Friday run", new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero));

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.Start(new DateTimeOffset(2026, 6, 2, 12, 5, 0, TimeSpan.Zero)));

        Assert.Equal("live_session_requires_session_teams", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
    }

    [Fact]
    public void Start_ClosesEnrollmentWindow_AndTransitionsToActive()
    {
        var liveSession = CreateLiveSession("Friday run", new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero));
        var joinCode = JoinCode.Parse("ABC234");
        var openedAtUtc = new DateTimeOffset(2026, 6, 2, 12, 1, 0, TimeSpan.Zero);
        var startedAtUtc = new DateTimeOffset(2026, 6, 2, 12, 5, 0, TimeSpan.Zero);
        liveSession.AssignJoinCode(joinCode);
        liveSession.OpenEnrollmentWindow(openedAtUtc);
        liveSession.RegisterTeam(Guid.NewGuid(), "Team Cave", "participant-1", joinCode, openedAtUtc);

        liveSession.Start(startedAtUtc);

        Assert.Equal(LiveSessionStates.Active, liveSession.State);
        Assert.Equal(startedAtUtc, liveSession.EnrollmentWindowClosedAtUtc);
    }

    [Fact]
    public void Pause_Resume_Finalize_FollowValidSequence()
    {
        var liveSession = CreateStartedLiveSession();

        liveSession.Pause();
        Assert.Equal(LiveSessionStates.Paused, liveSession.State);

        liveSession.Resume();
        Assert.Equal(LiveSessionStates.Active, liveSession.State);

        liveSession.FinalizeSession();
        Assert.Equal(LiveSessionStates.Finalized, liveSession.State);
    }

    [Fact]
    public void Cancel_RejectsFinalizedSession()
    {
        var liveSession = CreateStartedLiveSession();
        liveSession.FinalizeSession();

        var exception = Assert.Throws<UmbralDomainException>(() => liveSession.Cancel());

        Assert.Equal("live_session_cannot_cancel", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
    }

    private static LiveSession CreateStartedLiveSession()
    {
        var nowUtc = new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero);
        var joinCode = JoinCode.Parse("ABC234");
        var liveSession = CreateLiveSession("Started session", nowUtc);
        liveSession.AssignJoinCode(joinCode);
        liveSession.OpenEnrollmentWindow(nowUtc);
        liveSession.RegisterTeam(Guid.NewGuid(), "Team Cave", "participant-1", joinCode, nowUtc);
        liveSession.Start(nowUtc.AddMinutes(5));
        return liveSession;
    }

    private static LiveSession CreateLiveSession(string name, DateTimeOffset createdAtUtc)
    {
        return LiveSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Night Mission",
            name,
            scheduledStartAtUtc: null,
            createdAtUtc: createdAtUtc,
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
    }
}

public sealed class LiveSessionEndpointTests
{
    [Fact]
    public async Task CreateLiveSession_ReturnsCreatedSessionWithSelectedFlow()
    {
        await using var factory = new SessionOperationsApiFactory();
        factory.SetEligibleMission(new EligibleMissionForLiveSessionSnapshot(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Night Mission",
            [
                CreateMissionStageSnapshot(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    "Stage 1",
                    sessionStageOrder: 1,
                    sourceOrder: 10,
                    difficulty: "Easy"),
                CreateMissionStageSnapshot(
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    "Stage 2",
                    sessionStageOrder: 2,
                    sourceOrder: 20,
                    difficulty: "Hard")
            ]));
        var client = factory.CreateOperatorClient();

        var response = await client.PostAsJsonAsync(
            "/api/session-operations/live-sessions",
            new CreateLiveSessionRequest(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "Night Mission / Wave A",
                new DateTimeOffset(2026, 6, 3, 20, 30, 0, TimeSpan.Zero),
                [
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Guid.Parse("11111111-1111-1111-1111-111111111111")
                ]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var liveSession = await response.Content.ReadFromJsonAsync<LiveSessionResponse>();

        Assert.NotNull(liveSession);
        Assert.Equal("Night Mission / Wave A", liveSession.Name);
        Assert.Equal(LiveSessionStates.Scheduled, liveSession.State);
        Assert.Collection(
            liveSession.SessionStageFlow,
            first =>
            {
                Assert.Equal("Stage 2", first.Name);
                Assert.Equal("Prompt for Stage 2", first.Prompt);
                Assert.Equal(1, first.SessionStageOrder);
                Assert.Equal(20, first.SourceOrder);
                Assert.Equal("Hard", first.Difficulty);
            },
            second =>
            {
                Assert.Equal("Stage 1", second.Name);
                Assert.Equal("Prompt for Stage 1", second.Prompt);
                Assert.Equal(2, second.SessionStageOrder);
                Assert.Equal(10, second.SourceOrder);
                Assert.Equal("Easy", second.Difficulty);
            });
    }

    [Fact]
    public async Task CreateLiveSession_ReturnsConflict_WhenMissionIsInactive()
    {
        await using var factory = new SessionOperationsApiFactory();
        factory.SetMissionFailure(new UmbralDomainException(
            "mission_not_active",
            "Mission 'Night Mission' is not active.",
            UmbralFailureCategory.Conflict));
        var client = factory.CreateOperatorClient();

        var response = await client.PostAsJsonAsync(
            "/api/session-operations/live-sessions",
            new CreateLiveSessionRequest(
                Guid.NewGuid(),
                "Night Mission / Wave A",
                null,
                [Guid.NewGuid()]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateLiveSession_ReturnsBadRequest_WhenNoStageIsSelected()
    {
        await using var factory = new SessionOperationsApiFactory();
        factory.SetEligibleMission(new EligibleMissionForLiveSessionSnapshot(
            Guid.NewGuid(),
            "Night Mission",
            [CreateMissionStageSnapshot(Guid.NewGuid(), "Stage 1", 1, 1, "Medium")]));
        var client = factory.CreateOperatorClient();

        var response = await client.PostAsJsonAsync(
            "/api/session-operations/live-sessions",
            new CreateLiveSessionRequest(
                Guid.NewGuid(),
                "Night Mission / Wave A",
                null,
                Array.Empty<Guid>()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListLiveSessions_ReturnsLatestFirst()
    {
        await using var factory = new SessionOperationsApiFactory();
        await factory.SeedLiveSessionAsync(CreateLiveSession(
            "Older session",
            new DateTimeOffset(2026, 6, 2, 11, 0, 0, TimeSpan.Zero)));
        await factory.SeedLiveSessionAsync(CreateLiveSession(
            "Newest session",
            new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero)));
        var client = factory.CreateOperatorClient();

        var liveSessions = await client.GetFromJsonAsync<List<LiveSessionResponse>>(
            "/api/session-operations/live-sessions");

        Assert.NotNull(liveSessions);
        Assert.Collection(
            liveSessions,
            first => Assert.Equal("Newest session", first.Name),
            second => Assert.Equal("Older session", second.Name));
    }

    [Fact]
    public async Task LiveSessionRoutes_ReturnForbidden_ForParticipant()
    {
        await using var factory = new SessionOperationsApiFactory();
        var client = factory.CreateParticipantClient();

        var response = await client.GetAsync("/api/session-operations/live-sessions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LifecycleStart_ReturnsConflict_WhenNoTeamsRegistered()
    {
        await using var factory = new SessionOperationsApiFactory();
        var missionId = Guid.NewGuid();
        var missionStageId = Guid.NewGuid();
        factory.SetEligibleMission(CreateEligibleMission(missionId, missionStageId));
        var operatorClient = factory.CreateOperatorClient();

        var liveSession = await CreateLiveSessionAsync(operatorClient, missionId, missionStageId);

        var response = await operatorClient.PostAsync(
            $"/api/session-operations/live-sessions/{liveSession.Id}/lifecycle/start",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task LifecycleFlow_TransitionsState_ClosesEnrollmentWindow_AndNotifiesRealtime()
    {
        await using var factory = new SessionOperationsApiFactory();
        var missionId = Guid.NewGuid();
        var missionStageId = Guid.NewGuid();
        factory.SetEligibleMission(CreateEligibleMission(missionId, missionStageId));
        var operatorClient = factory.CreateOperatorClient();
        var participantClient = factory.CreateParticipantClient("participant-99");

        var liveSession = await CreateLiveSessionAsync(operatorClient, missionId, missionStageId);
        var joinCode = await GenerateJoinCodeAsync(operatorClient, liveSession.Id);
        var windowResponse = await operatorClient.PostAsync(
            $"/api/session-operations/live-sessions/{liveSession.Id}/session-enrollment/window/open",
            content: null);
        windowResponse.EnsureSuccessStatusCode();
        var registerResponse = await participantClient.PostAsJsonAsync(
            "/api/session-operations/session-enrollment/teams",
            new RegisterTeamRequest(joinCode, "Team Cave"));
        registerResponse.EnsureSuccessStatusCode();

        var startResponse = await operatorClient.PostAsync(
            $"/api/session-operations/live-sessions/{liveSession.Id}/lifecycle/start",
            content: null);
        var pauseResponse = await operatorClient.PostAsync(
            $"/api/session-operations/live-sessions/{liveSession.Id}/lifecycle/pause",
            content: null);
        var resumeResponse = await operatorClient.PostAsync(
            $"/api/session-operations/live-sessions/{liveSession.Id}/lifecycle/resume",
            content: null);
        var finalizeResponse = await operatorClient.PostAsync(
            $"/api/session-operations/live-sessions/{liveSession.Id}/lifecycle/finalize",
            content: null);

        startResponse.EnsureSuccessStatusCode();
        pauseResponse.EnsureSuccessStatusCode();
        resumeResponse.EnsureSuccessStatusCode();
        finalizeResponse.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SessionOperationsDbContext>();
        var storedSession = await dbContext.LiveSessions.SingleAsync(session => session.Id == liveSession.Id);
        Assert.Equal(LiveSessionStates.Finalized, storedSession.State);
        Assert.NotNull(storedSession.EnrollmentWindowClosedAtUtc);

        Assert.Collection(
            factory.RecordedStateChanges,
            first => Assert.Equal(LiveSessionStates.Active, first.State),
            second => Assert.Equal(LiveSessionStates.Paused, second.State),
            third => Assert.Equal(LiveSessionStates.Active, third.State),
            fourth => Assert.Equal(LiveSessionStates.Finalized, fourth.State));
    }

    private static EligibleMissionStageSnapshot CreateMissionStageSnapshot(
        Guid id,
        string name,
        int sessionStageOrder,
        int sourceOrder,
        string difficulty)
    {
        return new EligibleMissionStageSnapshot(
            id,
            name,
            sessionStageOrder,
            sourceOrder,
            30,
            difficulty,
            "Trivia",
            $"Prompt for {name}",
            null,
            "answer",
            null,
            []);
    }

    private static async Task<LiveSessionResponse> CreateLiveSessionAsync(
        HttpClient client,
        Guid missionId,
        Guid missionStageId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/session-operations/live-sessions",
            new CreateLiveSessionRequest(missionId, "Session Alpha", null, [missionStageId]));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LiveSessionResponse>())!;
    }

    private static async Task<string> GenerateJoinCodeAsync(HttpClient client, Guid liveSessionId)
    {
        var response = await client.PostAsync(
            $"/api/session-operations/live-sessions/{liveSessionId}/session-enrollment/join-code",
            content: null);
        response.EnsureSuccessStatusCode();
        var joinCode = await response.Content.ReadFromJsonAsync<GenerateJoinCodeResponse>();
        return joinCode!.JoinCode;
    }

    private static EligibleMissionForLiveSessionSnapshot CreateEligibleMission(Guid missionId, Guid missionStageId)
    {
        return new EligibleMissionForLiveSessionSnapshot(
            missionId,
            "Night Mission",
            [
                new EligibleMissionStageSnapshot(
                    missionStageId,
                    "Stage 1",
                    1,
                    1,
                    30,
                    "Medium",
                    "Trivia",
                    "Prompt for Stage 1",
                    null,
                    "answer",
                    null,
                    [])
            ]);
    }

    private static LiveSession CreateLiveSession(string name, DateTimeOffset createdAtUtc)
    {
        return LiveSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Night Mission",
            name,
            scheduledStartAtUtc: null,
            createdAtUtc: createdAtUtc,
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
    }
}

internal sealed class SessionOperationsApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"session-operations-tests-{Guid.NewGuid():N}";
    private readonly FakeMissionDesignLiveSessionCatalog missionDesignLiveSessionCatalog = new();
    private readonly FakeLiveSessionStateNotifier liveSessionStateNotifier = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=umbral;Username=umbral;Password=umbral",
                ["Auth:Authority"] = "http://localhost:8080/realms/umbral",
                ["Auth:Audience"] = "umbral-session-operations-api",
                ["RabbitMQ:Host"] = "localhost",
                ["SignalR:Enabled"] = "true",
                ["MissionDesign:BaseUrl"] = "http://mission-design-service:8080/",
                ["Persistence:ApplyMigrationsOnStartup"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });

            services.RemoveAll<DbContextOptions<SessionOperationsDbContext>>();
            services.RemoveAll<SessionOperationsDbContext>();
            services.AddDbContext<SessionOperationsDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));

            services.RemoveAll<IMissionDesignLiveSessionCatalog>();
            services.AddSingleton(missionDesignLiveSessionCatalog);
            services.AddSingleton<IMissionDesignLiveSessionCatalog>(missionDesignLiveSessionCatalog);
            services.RemoveAll<ILiveSessionStateNotifier>();
            services.AddSingleton(liveSessionStateNotifier);
            services.AddSingleton<ILiveSessionStateNotifier>(liveSessionStateNotifier);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero)));
        });
    }

    public IReadOnlyList<LiveSessionStateChangedEvent> RecordedStateChanges => liveSessionStateNotifier.Events;

    public HttpClient CreateOperatorClient()
    {
        return CreateClientForRole(UmbralRoles.Operator);
    }

    public HttpClient CreateParticipantClient(string participantUserId = "participant-1")
    {
        return CreateClientForRole(UmbralRoles.Participant, participantUserId);
    }

    public void SetEligibleMission(EligibleMissionForLiveSessionSnapshot eligibleMission)
    {
        missionDesignLiveSessionCatalog.SetEligibleMission(eligibleMission);
    }

    public void SetMissionFailure(UmbralServiceException exception)
    {
        missionDesignLiveSessionCatalog.SetFailure(exception);
    }

    public async Task SeedLiveSessionAsync(LiveSession liveSession)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SessionOperationsDbContext>();

        await dbContext.Database.EnsureCreatedAsync();
        dbContext.LiveSessions.Add(liveSession);
        await dbContext.SaveChangesAsync();
    }

    private HttpClient CreateClientForRole(string role, string userId = "operator-1")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, role);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserIdHeaderName, userId);

        return client;
    }

    private sealed class FakeMissionDesignLiveSessionCatalog : IMissionDesignLiveSessionCatalog
    {
        private EligibleMissionForLiveSessionSnapshot? eligibleMission;
        private UmbralServiceException? failure;

        public Task<EligibleMissionForLiveSessionSnapshot> GetEligibleMissionForLiveSessionAsync(
            Guid missionId,
            CancellationToken cancellationToken)
        {
            if (failure is not null)
            {
                throw failure;
            }

            return Task.FromResult(
                eligibleMission ?? throw new InvalidOperationException("Test mission snapshot was not configured."));
        }

        public void SetEligibleMission(EligibleMissionForLiveSessionSnapshot nextEligibleMission)
        {
            eligibleMission = nextEligibleMission;
            failure = null;
        }

        public void SetFailure(UmbralServiceException exception)
        {
            failure = exception;
            eligibleMission = null;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }

    private sealed class FakeLiveSessionStateNotifier : ILiveSessionStateNotifier
    {
        private readonly List<LiveSessionStateChangedEvent> events = [];

        public IReadOnlyList<LiveSessionStateChangedEvent> Events => events;

        public Task NotifyStateChangedAsync(
            LiveSessionStateChangedEvent stateChangedEvent,
            CancellationToken cancellationToken)
        {
            events.Add(stateChangedEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string RoleHeaderName = "X-Test-Role";
        public const string UserIdHeaderName = "X-Test-User-Id";

        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(RoleHeaderName))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = Request.Headers[RoleHeaderName]
                .Where(static role => !string.IsNullOrWhiteSpace(role))
                .Select(role => new Claim(ClaimTypes.Role, role!))
                .ToList();
            var userId = Request.Headers.TryGetValue(UserIdHeaderName, out var userIdValues)
                ? userIdValues.FirstOrDefault()
                : "test-user";
            if (!string.IsNullOrWhiteSpace(userId))
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId!));
                claims.Add(new Claim("sub", userId!));
            }

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
