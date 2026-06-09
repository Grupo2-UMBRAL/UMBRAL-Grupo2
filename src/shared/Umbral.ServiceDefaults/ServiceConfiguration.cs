using Microsoft.Extensions.Configuration;

namespace Umbral.ServiceDefaults;

public static class ServiceConfiguration
{
    public static AuthConfiguration GetRequiredAuthConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var authority = configuration["Auth:Authority"];
        var audience = configuration["Auth:Audience"];
        var requireHttpsMetadata = configuration.GetValue("Auth:RequireHttpsMetadata", false);

        if (string.IsNullOrWhiteSpace(authority))
        {
            throw new InvalidOperationException("Missing required configuration value 'Auth:Authority'.");
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException("Missing required configuration value 'Auth:Audience'.");
        }

        return new AuthConfiguration(authority, audience, requireHttpsMetadata);
    }

    public static string GetRequiredPostgresConnectionString(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Missing required connection string 'ConnectionStrings:Postgres'.");
        }

        return connectionString;
    }
}
