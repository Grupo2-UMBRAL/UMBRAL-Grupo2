using System.Net.Http.Headers;
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
using MissionManagement.Domain.Missions;
using MissionManagement.Infrastructure.Persistence;
using Umbral.ServiceDefaults;

namespace MissionManagement.IntegrationTests;

internal sealed class MissionApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"mission-management-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=umbral;Username=umbral;Password=umbral",
                ["Auth:Authority"] = "http://localhost:8080/realms/umbral",
                ["Auth:Audience"] = "umbral-mission-management-api",
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

            services.RemoveAll<DbContextOptions<MissionManagementDbContext>>();
            services.RemoveAll<MissionManagementDbContext>();

            services.AddScoped<MissionManagementDbContext>(sp =>
            {
                var options = new DbContextOptionsBuilder<MissionManagementDbContext>()
                    .UseInMemoryDatabase(databaseName)
                    .Options;
                return new MissionManagementDbContext(options);
            });

            services.AddScoped<DbContextOptions<MissionManagementDbContext>>(sp =>
            {
                return new DbContextOptionsBuilder<MissionManagementDbContext>()
                    .UseInMemoryDatabase(databaseName)
                    .Options;
            });
        });
    }

    public HttpClient CreateAuthorizedClient() => CreateClientForRole(UmbralRoles.Administrator);

    public HttpClient CreateOperatorClient() => CreateClientForRole(UmbralRoles.Operator);

    public HttpClient CreateParticipantClient() => CreateClientForRole(UmbralRoles.Participant);

    public async Task SeedMissionAsync(Mission mission)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MissionManagementDbContext>();

        await dbContext.Database.EnsureCreatedAsync();
        dbContext.Missions.Add(mission);
        foreach (var item in EnumerateDepthFirst(mission.RootItems))
        {
            dbContext.PathItems.Add(item);
        }

        await dbContext.SaveChangesAsync();
    }

    private static IEnumerable<PathItem> EnumerateDepthFirst(IReadOnlyList<PathItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            if (item is Section section)
            {
                foreach (var child in EnumerateDepthFirst(section.Children))
                {
                    yield return child;
                }
            }
        }
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
