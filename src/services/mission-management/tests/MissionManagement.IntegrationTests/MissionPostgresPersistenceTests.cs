using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionManagement.Application.Features.Missions;
using MissionManagement.Domain.Missions;
using MissionManagement.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Umbral.ServiceDefaults;
using Xunit;

namespace MissionManagement.IntegrationTests;

/// <summary>
/// Persistence tests against a REAL Postgres container (Testcontainers), exercising the
/// production EF Core model and the real migration — FK constraints, restrict/cascade rules,
/// SQL translation — none of which the InMemory-backed <see cref="MissionEndpointTests"/> can
/// catch. The DbContext is repointed at the container in <c>ConfigureServices</c> (the only
/// hook that runs after the app's own registration), then the real migration is applied; the
/// cases then drive the production persistence path through the HTTP API.
///
/// Auto-skips (no-op) when no Docker engine is reachable; CI integration lanes have Docker.
/// One container per test keeps cases isolated; switch to a collection fixture if this grows.
/// </summary>
public sealed class MissionPostgresPersistenceTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private bool dockerAvailable;
    private PostgresMissionApiFactory? factory;

    public async Task InitializeAsync()
    {
        try
        {
            await container.StartAsync();
        }
        catch (Exception)
        {
            // No reachable Docker engine -> the tests below no-op. (Migration/connection
            // failures are NOT swallowed here -- only the container start is guarded.)
            dockerAvailable = false;
            return;
        }

        dockerAvailable = true;
        factory = new PostgresMissionApiFactory(container.GetConnectionString());

        // Apply the real production migration to the fresh container before the cases run.
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MissionManagementDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        factory?.Dispose();
        if (dockerAvailable)
        {
            await container.DisposeAsync();
        }
    }

    [Fact]
    public async Task CreateMission_PersistsAndReadsBackSectionTree_AgainstRealPostgres()
    {
        if (!dockerAvailable)
        {
            return;
        }

        var client = factory!.CreateAdminClient();

        var request = new CreateMissionRequest(
            "Postgres Circuit",
            "Persisted on real Postgres.",
            75,
            new[]
            {
                new MissionItemRequest(
                    Kind: MissionItemKind.Section,
                    Order: 1,
                    Title: "Downtown",
                    Children: new[]
                    {
                        new MissionItemRequest(
                            Kind: MissionItemKind.Challenge,
                            Order: 1,
                            Title: "Fountain Hunt",
                            GameType: MissionGameType.TreasureHunt,
                            Difficulty: Difficulty.Medium,
                            TimeLimitMinutes: 15,
                            Searches: new[]
                            {
                                new SearchRequest(null, 1, "Find the fountain", "qr-1", null, null,
                                    new[] { new HintRequest(null, 1, "Near the plaza", false, null, null) })
                            })
                    })
            });

        var createResponse = await client.PostAsJsonAsync("/api/mission-management/missions", request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<MissionResponse>(JsonOptions);
        Assert.NotNull(created);

        // Fresh GET -> proves the tree round-tripped through Postgres, not an in-request cache.
        var detail = await client.GetFromJsonAsync<MissionResponse>(
            $"/api/mission-management/missions/{created!.Id}", JsonOptions);

        Assert.NotNull(detail);
        var section = Assert.Single(detail!.Items);
        Assert.Equal("Downtown", section.Title);
        var challenge = Assert.Single(section.Children!);
        Assert.Equal(MissionGameType.TreasureHunt, challenge.GameType);
        var search = Assert.Single(challenge.Searches!);
        Assert.Equal("qr-1", search.ExpectedQrHash);
        Assert.Equal("Near the plaza", Assert.Single(search.Hints).Content);
    }

    [Fact]
    public async Task ActivateMission_PersistsActiveState_AgainstRealPostgres()
    {
        if (!dockerAvailable)
        {
            return;
        }

        var client = factory!.CreateAdminClient();

        var request = new CreateMissionRequest(
            "Activatable",
            "Has an eligible play.",
            40,
            new[]
            {
                new MissionItemRequest(
                    Kind: MissionItemKind.Challenge,
                    Order: 1,
                    Title: "Gate Hunt",
                    GameType: MissionGameType.TreasureHunt,
                    Difficulty: Difficulty.Easy,
                    TimeLimitMinutes: 20,
                    Searches: new[]
                    {
                        new SearchRequest(null, 1, "Scan the gate", "qr-gate", null, null, null)
                    })
            });

        var created = await (await client.PostAsJsonAsync("/api/mission-management/missions", request))
            .Content.ReadFromJsonAsync<MissionResponse>(JsonOptions);
        Assert.NotNull(created);

        var activateResponse = await client.PostAsync(
            $"/api/mission-management/missions/{created!.Id}/activate", content: null);
        activateResponse.EnsureSuccessStatusCode();

        var detail = await client.GetFromJsonAsync<MissionResponse>(
            $"/api/mission-management/missions/{created.Id}", JsonOptions);
        Assert.True(detail!.IsActive);
    }

    private sealed class PostgresMissionApiFactory : WebApplicationFactory<Program>
    {
        private readonly string connectionString;

        public PostgresMissionApiFactory(string connectionString) => this.connectionString = connectionString;

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:Authority"] = "http://localhost:8080/realms/umbral",
                    ["Auth:Audience"] = "umbral-mission-management-api"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = PostgresTestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = PostgresTestAuthHandler.SchemeName;
                        options.DefaultScheme = PostgresTestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, PostgresTestAuthHandler>(
                        PostgresTestAuthHandler.SchemeName, _ => { });

                // Repoint the DbContext from the dev connection (read at registration time)
                // to the throwaway container. This is the only hook that runs late enough.
                services.RemoveAll<DbContextOptions<MissionManagementDbContext>>();
                services.RemoveAll<MissionManagementDbContext>();
                services.AddScoped<MissionManagementDbContext>(_ => new MissionManagementDbContext(BuildOptions()));
                services.AddScoped<DbContextOptions<MissionManagementDbContext>>(_ => BuildOptions());
            });
        }

        private DbContextOptions<MissionManagementDbContext> BuildOptions() =>
            new DbContextOptionsBuilder<MissionManagementDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        public HttpClient CreateAdminClient()
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(PostgresTestAuthHandler.SchemeName);
            client.DefaultRequestHeaders.Add(PostgresTestAuthHandler.RoleHeaderName, UmbralRoles.Administrator);
            return client;
        }
    }

    // NOTE: a near-duplicate of the InMemory factory's handler. Extracting a shared
    // Umbral.TestSupport (public TestAuthenticationHandler + a Postgres collection fixture)
    // is the natural follow-up once a second service gets real-Postgres tests.
    private sealed class PostgresTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string RoleHeaderName = "X-Test-Role";

        public PostgresTestAuthHandler(
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
