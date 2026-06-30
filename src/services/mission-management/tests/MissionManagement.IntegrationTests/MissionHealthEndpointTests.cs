using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace MissionManagement.IntegrationTests;

public sealed class MissionManagementHealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public MissionManagementHealthEndpointTests(WebApplicationFactory<Program> factory)
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
