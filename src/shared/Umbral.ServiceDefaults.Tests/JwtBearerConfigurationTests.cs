using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbral.ServiceDefaults;
using Xunit;

namespace Umbral.ServiceDefaults.Tests;

public sealed class JwtBearerConfigurationTests
{
    [Fact]
    public void AddUmbralApiDefaults_ConfiguresJwtValidationAtServiceBoundary()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "http://localhost:8080/realms/umbral",
                ["Auth:Audience"] = "umbral-session-management-api",
                ["Auth:RequireHttpsMetadata"] = "false"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddUmbralApiDefaults(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var jwtBearerOptions = serviceProvider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal("http://localhost:8080/realms/umbral", jwtBearerOptions.Authority);
        Assert.Equal("umbral-session-management-api", jwtBearerOptions.TokenValidationParameters.ValidAudience);
        Assert.True(jwtBearerOptions.TokenValidationParameters.ValidateAudience);
        Assert.False(jwtBearerOptions.RequireHttpsMetadata);
    }
}
