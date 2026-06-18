using Microsoft.Extensions.Configuration;
using Umbral.ServiceDefaults;
using Xunit;

namespace Umbral.ServiceDefaults.Tests;

public sealed class ServiceConfigurationTests
{
    [Fact]
    public void GetRequiredAuthConfiguration_ReturnsTypedValues_WhenSectionIsComplete()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Auth:Authority"] = "http://localhost:8080/realms/umbral",
            ["Auth:Audience"] = "umbral-session-management-api",
            ["Auth:RequireHttpsMetadata"] = "false"
        });

        var authConfiguration = ServiceConfiguration.GetRequiredAuthConfiguration(configuration);

        Assert.Equal("http://localhost:8080/realms/umbral", authConfiguration.Authority);
        Assert.Equal("umbral-session-management-api", authConfiguration.Audience);
        Assert.False(authConfiguration.RequireHttpsMetadata);
    }

    [Fact]
    public void GetRequiredAuthConfiguration_Throws_WhenAuthorityIsMissing()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Auth:Audience"] = "umbral-session-management-api"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            () => ServiceConfiguration.GetRequiredAuthConfiguration(configuration));

        Assert.Equal("Missing required configuration value 'Auth:Authority'.", exception.Message);
    }

    [Fact]
    public void GetRequiredPostgresConnectionString_Throws_WhenConnectionStringIsMissing()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>());

        var exception = Assert.Throws<InvalidOperationException>(
            () => ServiceConfiguration.GetRequiredPostgresConnectionString(configuration));

        Assert.Equal("Missing required connection string 'ConnectionStrings:Postgres'.", exception.Message);
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
