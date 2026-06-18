using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Umbral.ServiceDefaults;
using Xunit;

namespace ScoringAudit.Api.Tests;

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
        var client = factory.WithWebHostBuilder(builder =>
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
        }).CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }
}
