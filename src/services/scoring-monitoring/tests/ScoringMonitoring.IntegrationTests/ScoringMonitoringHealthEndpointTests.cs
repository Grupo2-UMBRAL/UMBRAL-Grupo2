using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Umbral.ServiceDefaults;
using Xunit;

namespace ScoringMonitoring.IntegrationTests;

public sealed class ScoringMonitoringHealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public ScoringMonitoringHealthEndpointTests(WebApplicationFactory<Program> factory)
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
                    ["Auth:Audience"] = "umbral-scoring-monitoring-api",
                    ["RabbitMQ:Host"] = "localhost",
                    ["Persistence:ApplyMigrationsOnStartup"] = "false"
                });
            });
        }).CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }
}
