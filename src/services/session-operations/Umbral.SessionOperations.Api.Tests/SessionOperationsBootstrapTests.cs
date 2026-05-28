using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Umbral.SessionOperations.Api.Application.Bootstrap.Queries;
using Umbral.SessionOperations.Api.Infrastructure;
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
