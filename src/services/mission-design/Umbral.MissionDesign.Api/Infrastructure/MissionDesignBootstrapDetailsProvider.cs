using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Infrastructure;

public sealed class MissionDesignBootstrapDetailsProvider(IConfiguration configuration) : IServiceBootstrapDetailsProvider
{
    public ServiceBootstrapDetails GetBootstrapDetails()
    {
        var authConfiguration = ServiceConfiguration.GetRequiredAuthConfiguration(configuration);

        return new ServiceBootstrapDetails(
            "Mission Design",
            DatabaseConfigured: !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Postgres")),
            Authority: authConfiguration.Authority,
            Audience: authConfiguration.Audience,
            MigrationsApplyOnStartup: configuration.GetValue("Persistence:ApplyMigrationsOnStartup", false));
    }
}
