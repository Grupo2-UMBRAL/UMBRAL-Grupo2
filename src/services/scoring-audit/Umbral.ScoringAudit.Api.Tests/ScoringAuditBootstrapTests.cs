using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Umbral.ScoringAudit.Api.Application.Bootstrap.Queries;
using Umbral.ScoringAudit.Api.Infrastructure;
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
