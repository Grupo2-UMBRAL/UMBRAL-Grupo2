using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Umbral.ScoringAudit.Api.Application.Bootstrap.Queries;
using Umbral.ScoringAudit.Api.Infrastructure;
using Umbral.ServiceDefaults;
using Xunit;

namespace Umbral.ScoringAudit.Api.Tests;

public sealed class ScoringAuditBootstrapDetailsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsConfiguredBootstrapDetails()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=umbral;Username=umbral;Password=umbral",
                ["Auth:Authority"] = "http://localhost:8080/realms/umbral",
                ["Auth:Audience"] = "umbral-scoring-audit-api",
                ["RabbitMQ:Host"] = "localhost",
                ["Persistence:ApplyMigrationsOnStartup"] = "true"
            })
            .Build();

        var provider = new ScoringAuditBootstrapDetailsProvider(configuration);
        var handler = new GetScoringAuditBootstrapDetailsQueryHandler(provider);

        var details = await handler.Handle(new GetScoringAuditBootstrapDetailsQuery(), CancellationToken.None);

        Assert.True(details.DatabaseConfigured);
        Assert.Equal("localhost", details.RabbitMqHost);
        Assert.True(details.MigrationsApplyOnStartup);
    }
}

public sealed class ScoringAuditHealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public ScoringAuditHealthEndpointTests(WebApplicationFactory<Program> factory)
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

public sealed class ScoringAuditRoleSmokeRouteTests
{
    [Fact]
    public async Task SmokeRoute_ReturnsUnauthorized_WhenRequestHasNoIdentity()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/scoring-audit/smoke/participant");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SmokeRoute_ReturnsForbidden_WhenIdentityHasWrongRole()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, UmbralRoles.Administrator);

        var response = await client.GetAsync("/api/scoring-audit/smoke/participant");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SmokeRoute_ReturnsSuccess_WhenIdentityHasRequiredRole()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, UmbralRoles.Participant);

        var response = await client.GetAsync("/api/scoring-audit/smoke/participant");

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
                        ["Auth:Audience"] = "umbral-scoring-audit-api",
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
                });
            });

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
