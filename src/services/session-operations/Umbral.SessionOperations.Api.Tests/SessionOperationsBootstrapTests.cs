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
                    "Trivia")
            ]);

        Assert.Equal(LiveSessionStates.Scheduled, liveSession.State);
        Assert.Single(liveSession.SessionStageFlow);
        Assert.Equal("Stage 1", liveSession.SessionStageFlow[0].Name);
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
                Assert.Equal(1, first.SessionStageOrder);
                Assert.Equal(20, first.SourceOrder);
                Assert.Equal("Hard", first.Difficulty);
            },
            second =>
            {
                Assert.Equal("Stage 1", second.Name);
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
            null,
            "answer",
            null,
            []);
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
                    "Trivia")
            ]);
    }
}

internal sealed class SessionOperationsApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"session-operations-tests-{Guid.NewGuid():N}";
    private readonly FakeMissionDesignLiveSessionCatalog missionDesignLiveSessionCatalog = new();

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
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero)));
        });
    }

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
