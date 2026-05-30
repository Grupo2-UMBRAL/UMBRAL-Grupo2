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
using Umbral.MissionDesign.Api.Application.Missions;
using Umbral.MissionDesign.Api.Domain.Missions;
using Umbral.MissionDesign.Api.Infrastructure;
using Umbral.ServiceDefaults;
using Xunit;

namespace Umbral.MissionDesign.Api.Tests;

public sealed class MissionDomainTests
{
    [Fact]
    public void Create_NormalizesSupportedGameType_AndMarksMissionActive()
    {
        var mission = Mission.Create(
            Guid.NewGuid(),
            "  Caracas Chase  ",
            " Urban treasure route ",
            " Medium ",
            90,
            "trivia");

        Assert.Equal("Caracas Chase", mission.Name);
        Assert.Equal("Urban treasure route", mission.Description);
        Assert.Equal("Medium", mission.Difficulty);
        Assert.Equal(MissionGameType.Trivia, mission.GameType);
        Assert.True(mission.IsActive);
    }

    [Fact]
    public void Create_RejectsUnsupportedGameType()
    {
        var exception = Assert.Throws<UmbralDomainException>(() =>
            Mission.Create(
                Guid.NewGuid(),
                "Night Run",
                "Description",
                "Hard",
                45,
                "Race"));

        Assert.Equal("mission_game_type_unsupported", exception.Code);
        Assert.Equal(UmbralFailureCategory.Validation, exception.Category);
    }

    [Fact]
    public void Deactivate_RejectsAlreadyInactiveMission()
    {
        var mission = Mission.Create(
            Guid.NewGuid(),
            "Museum Hunt",
            "Description",
            "Easy",
            30,
            MissionGameType.TreasureHunt);

        mission.Deactivate();

        var exception = Assert.Throws<UmbralDomainException>(mission.Deactivate);

        Assert.Equal("mission_already_inactive", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
    }
}

public sealed class MissionEndpointTests
{
    [Fact]
    public async Task CreateMission_ReturnsCreatedMission()
    {
        await using var factory = new MissionApiFactory();
        var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsJsonAsync(
            "/api/mission-design/missions",
            new CreateMissionRequest(
                "City Circuit",
                "Route through control points.",
                "Medium",
                75,
                MissionGameType.TreasureHunt));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var mission = await response.Content.ReadFromJsonAsync<MissionResponse>();

        Assert.NotNull(mission);
        Assert.Equal("City Circuit", mission.Name);
        Assert.Equal(MissionGameType.TreasureHunt, mission.GameType);
        Assert.True(mission.IsActive);
    }

    [Fact]
    public async Task ListMissions_ReturnsOrderedSummaries()
    {
        await using var factory = new MissionApiFactory();
        await factory.SeedMissionAsync(Mission.Create(
            Guid.NewGuid(),
            "Zulu Mission",
            "Last mission in list.",
            "Hard",
            120,
            MissionGameType.Trivia));
        await factory.SeedMissionAsync(Mission.Create(
            Guid.NewGuid(),
            "Alpha Mission",
            "First mission in list.",
            "Easy",
            30,
            MissionGameType.TreasureHunt));
        var client = factory.CreateAuthorizedClient();

        var missions = await client.GetFromJsonAsync<List<MissionSummaryResponse>>("/api/mission-design/missions");

        Assert.NotNull(missions);
        Assert.Collection(
            missions,
            first => Assert.Equal("Alpha Mission", first.Name),
            second => Assert.Equal("Zulu Mission", second.Name));
    }

    [Fact]
    public async Task UpdateMission_ReturnsUpdatedMission()
    {
        await using var factory = new MissionApiFactory();
        var existingMission = Mission.Create(
            Guid.NewGuid(),
            "Old Mission",
            "Old description.",
            "Medium",
            45,
            MissionGameType.Trivia);
        await factory.SeedMissionAsync(existingMission);
        var client = factory.CreateAuthorizedClient();

        var response = await client.PutAsJsonAsync(
            $"/api/mission-design/missions/{existingMission.Id}",
            new UpdateMissionRequest(
                "Updated Mission",
                "Updated description.",
                "Hard",
                95));

        response.EnsureSuccessStatusCode();

        var mission = await response.Content.ReadFromJsonAsync<MissionResponse>();

        Assert.NotNull(mission);
        Assert.Equal("Updated Mission", mission.Name);
        Assert.Equal("Updated description.", mission.Description);
        Assert.Equal("Hard", mission.Difficulty);
        Assert.Equal(95, mission.MaximumDurationMinutes);
        Assert.Equal(MissionGameType.Trivia, mission.GameType);
    }

    [Fact]
    public async Task GetMissionById_ReturnsMissionDetail()
    {
        await using var factory = new MissionApiFactory();
        var existingMission = Mission.Create(
            Guid.NewGuid(),
            "Checkpoint Mission",
            "Detailed mission.",
            "Medium",
            55,
            MissionGameType.TreasureHunt);
        await factory.SeedMissionAsync(existingMission);
        var client = factory.CreateAuthorizedClient();

        var mission = await client.GetFromJsonAsync<MissionResponse>(
            $"/api/mission-design/missions/{existingMission.Id}");

        Assert.NotNull(mission);
        Assert.Equal(existingMission.Id, mission.Id);
        Assert.Equal("Checkpoint Mission", mission.Name);
        Assert.Equal("Detailed mission.", mission.Description);
    }

    [Fact]
    public async Task DeactivateMission_ReturnsInactiveMission()
    {
        await using var factory = new MissionApiFactory();
        var mission = Mission.Create(
            Guid.NewGuid(),
            "Deactivation Mission",
            "Description.",
            "Easy",
            25,
            MissionGameType.TreasureHunt);
        await factory.SeedMissionAsync(mission);
        var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsync(
            $"/api/mission-design/missions/{mission.Id}/deactivate",
            content: null);

        response.EnsureSuccessStatusCode();

        var updatedMission = await response.Content.ReadFromJsonAsync<MissionResponse>();

        Assert.NotNull(updatedMission);
        Assert.False(updatedMission.IsActive);
    }

    [Fact]
    public async Task DeactivateMission_ReturnsConflict_WhenMissionAlreadyInactive()
    {
        await using var factory = new MissionApiFactory();
        var mission = Mission.Create(
            Guid.NewGuid(),
            "Dormant Mission",
            "Description.",
            "Easy",
            25,
            MissionGameType.TreasureHunt);
        mission.Deactivate();
        await factory.SeedMissionAsync(mission);
        var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsync(
            $"/api/mission-design/missions/{mission.Id}/deactivate",
            content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}

internal sealed class MissionApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"mission-design-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=umbral;Username=umbral;Password=umbral",
                ["Auth:Authority"] = "http://localhost:8080/realms/umbral",
                ["Auth:Audience"] = "umbral-mission-design-api",
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

            services.RemoveAll<DbContextOptions<MissionDesignDbContext>>();
            services.RemoveAll<MissionDesignDbContext>();
            services.AddDbContext<MissionDesignDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
        });
    }

    public HttpClient CreateAuthorizedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, UmbralRoles.Administrator);

        return client;
    }

    public async Task SeedMissionAsync(Mission mission)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MissionDesignDbContext>();

        await dbContext.Database.EnsureCreatedAsync();
        dbContext.Missions.Add(mission);
        await dbContext.SaveChangesAsync();
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
