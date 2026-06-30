using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScoringMonitoring.Application.Features.Rankings;
using ScoringMonitoring.Application.Features.Scoreboards.Commands.ApplyPenalty;
using ScoringMonitoring.Application.Features.Scoreboards.Commands.RecordStageCredit;
using ScoringMonitoring.Application.Features.SessionEventLogs;
using ScoringMonitoring.Infrastructure.Persistence;
using Umbral.ServiceDefaults;
using Xunit;

namespace ScoringMonitoring.IntegrationTests;

public sealed class ScoringMonitoringControllerTests
{
    [Fact]
    public async Task RecordStageCredit_PersistsScoreEntryAndReturnsRanking()
    {
        await using var factory = new ScoringApiFactory();
        var client = factory.CreateOperatorClient();
        var liveSessionId = Guid.NewGuid();
        var sessionTeamId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            $"/api/scoring-monitoring/sessions/{liveSessionId}/scores",
            new RecordStageCreditRequest(
                sessionTeamId,
                PlayId: Guid.NewGuid(),
                Difficulty: "Medium",
                ResolutionTime: TimeSpan.FromSeconds(15),
                RecordedAt: DateTimeOffset.Parse("2026-06-04T01:45:00Z"),
                ValidationOverride: false));

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RecordStageCreditResponse>();

        Assert.NotNull(payload);
        Assert.Equal(liveSessionId, payload.LiveSessionId);
        Assert.Equal(sessionTeamId, payload.SessionTeamId);
        Assert.True(payload.ScoreEntryCreated);
        Assert.Equal(200, payload.VisibleScore);
        Assert.Equal(liveSessionId, payload.Ranking.LiveSessionId);
        Assert.Equal(sessionTeamId, Assert.Single(payload.Ranking.Items).SessionTeamId);
    }

    [Fact]
    public async Task ApplyPenalty_RecordsPenaltyAndReturnsRanking()
    {
        await using var factory = new ScoringApiFactory();
        var client = factory.CreateOperatorClient();
        var liveSessionId = Guid.NewGuid();
        var sessionTeamId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            $"/api/scoring-monitoring/sessions/{liveSessionId}/penalties",
            new ApplyPenaltyRequest(
                sessionTeamId,
                CommandId: Guid.NewGuid(),
                Severity: "Major",
                AppliedByOperatorUserId: "operator-7",
                Reason: "Uso indebido de pista.",
                RecordedAt: DateTimeOffset.Parse("2026-06-04T02:15:00Z")));

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ApplyPenaltyResponse>();

        Assert.NotNull(payload);
        Assert.Equal(liveSessionId, payload.LiveSessionId);
        Assert.True(payload.PenaltyApplied);
        Assert.NotNull(payload.PenaltyId);
        Assert.Equal(0, payload.VisibleScore);
        Assert.Equal(liveSessionId, payload.Ranking.LiveSessionId);
    }

    [Fact]
    public async Task GetRanking_ReturnsRankingForLiveSession()
    {
        await using var factory = new ScoringApiFactory();
        var client = factory.CreateOperatorClient();
        var liveSessionId = Guid.NewGuid();
        var sessionTeamId = Guid.NewGuid();

        await client.PostAsJsonAsync(
            $"/api/scoring-monitoring/sessions/{liveSessionId}/scores",
            new RecordStageCreditRequest(
                sessionTeamId,
                PlayId: Guid.NewGuid(),
                Difficulty: "Medium",
                ResolutionTime: TimeSpan.FromSeconds(15),
                RecordedAt: DateTimeOffset.Parse("2026-06-04T01:45:00Z"),
                ValidationOverride: false));

        var ranking = await client.GetFromJsonAsync<RankingPayload>(
            $"/api/scoring-monitoring/sessions/{liveSessionId}/ranking");

        Assert.NotNull(ranking);
        Assert.Equal(liveSessionId, ranking.LiveSessionId);
        var item = Assert.Single(ranking.Items);
        Assert.Equal(sessionTeamId, item.SessionTeamId);
        Assert.Equal(200, item.VisibleScore);
    }

    [Fact]
    public async Task LogSessionEvent_PersistsEventReturnedByEventLogQuery()
    {
        await using var factory = new ScoringApiFactory();
        var client = factory.CreateOperatorClient();
        var liveSessionId = Guid.NewGuid();

        var logResponse = await client.PostAsJsonAsync(
            $"/api/scoring-monitoring/sessions/{liveSessionId}/event-log",
            new LogSessionEventRequest("OperatorNote", "Operador marco control manual."));

        logResponse.EnsureSuccessStatusCode();

        var logged = await logResponse.Content.ReadFromJsonAsync<SessionEventLogPayload>();
        Assert.NotNull(logged);
        Assert.Equal("OperatorNote", logged.EventType);

        var eventLog = await client.GetFromJsonAsync<List<SessionEventLogPayload>>(
            $"/api/scoring-monitoring/sessions/{liveSessionId}/event-log");

        Assert.NotNull(eventLog);
        var entry = Assert.Single(eventLog);
        Assert.Equal(logged.Id, entry.Id);
        Assert.Equal("Operador marco control manual.", entry.Description);
    }

    [Fact]
    public async Task GetEventLog_ReturnsEmptyListWhenSessionHasNoEvents()
    {
        await using var factory = new ScoringApiFactory();
        var client = factory.CreateOperatorClient();

        var eventLog = await client.GetFromJsonAsync<List<SessionEventLogPayload>>(
            $"/api/scoring-monitoring/sessions/{Guid.NewGuid()}/event-log");

        Assert.NotNull(eventLog);
        Assert.Empty(eventLog);
    }

    [Fact]
    public async Task ScoringRoutes_ReturnForbidden_ForParticipant()
    {
        await using var factory = new ScoringApiFactory();
        var client = factory.CreateParticipantClient();

        var response = await client.GetAsync(
            $"/api/scoring-monitoring/sessions/{Guid.NewGuid()}/event-log");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ScoringRoutes_ReturnUnauthorized_WhenAnonymous()
    {
        await using var factory = new ScoringApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/scoring-monitoring/sessions/{Guid.NewGuid()}/ranking");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

internal sealed class ScoringApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"scoring-monitoring-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=umbral;Username=umbral;Password=umbral",
                ["Auth:Authority"] = "http://localhost:8080/realms/umbral",
                ["Auth:Audience"] = "umbral-scoring-monitoring-api",
                ["RabbitMQ:Host"] = "localhost",
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

            services.RemoveAll<DbContextOptions<ScoringMonitoringDbContext>>();
            services.RemoveAll<ScoringMonitoringDbContext>();

            services.AddScoped<ScoringMonitoringDbContext>(_ =>
            {
                var options = new DbContextOptionsBuilder<ScoringMonitoringDbContext>()
                    .UseInMemoryDatabase(databaseName)
                    .Options;
                return new ScoringMonitoringDbContext(options);
            });

            services.AddScoped<DbContextOptions<ScoringMonitoringDbContext>>(_ =>
                new DbContextOptionsBuilder<ScoringMonitoringDbContext>()
                    .UseInMemoryDatabase(databaseName)
                    .Options);
        });
    }

    public HttpClient CreateOperatorClient()
    {
        return CreateClientForRole(UmbralRoles.Operator);
    }

    public HttpClient CreateParticipantClient()
    {
        return CreateClientForRole(UmbralRoles.Participant);
    }

    private HttpClient CreateClientForRole(string role)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, role);

        return client;
    }

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string RoleHeaderName = "X-Test-Role";

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
                .Select(role => new Claim(ClaimTypes.Role, role!));
            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
