using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Umbral.MissionDesign.Api.Application.Bootstrap.Queries;
using Umbral.MissionDesign.Api.Infrastructure;
using Xunit;

namespace Umbral.MissionDesign.Api.Tests;

public sealed class MissionDesignBootstrapDetailsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsConfiguredBootstrapDetails()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=umbral;Username=umbral;Password=umbral",
                ["Auth:Authority"] = "http://localhost:8080/realms/umbral",
                ["Auth:Audience"] = "umbral-mission-design-api",
                ["Persistence:ApplyMigrationsOnStartup"] = "true"
            })
            .Build();

        var provider = new MissionDesignBootstrapDetailsProvider(configuration);
        var handler = new GetMissionDesignBootstrapDetailsQueryHandler(provider);

        var details = await handler.Handle(new GetMissionDesignBootstrapDetailsQuery(), CancellationToken.None);

        Assert.True(details.DatabaseConfigured);
        Assert.True(details.MigrationsApplyOnStartup);
        Assert.Equal("umbral-mission-design-api", details.Audience);
        Assert.Contains("technical=500", details.ErrorMapping);
    }
}

public sealed class MissionDesignHealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public MissionDesignHealthEndpointTests(WebApplicationFactory<Program> factory)
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
