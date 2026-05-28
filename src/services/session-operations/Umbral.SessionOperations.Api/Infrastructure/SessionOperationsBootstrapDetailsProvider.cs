using Umbral.ServiceDefaults;

namespace Umbral.SessionOperations.Api.Infrastructure;

public sealed class SessionOperationsBootstrapDetailsProvider(IConfiguration configuration) : IServiceBootstrapDetailsProvider
{
    public ServiceBootstrapDetails GetBootstrapDetails()
    {
        var authConfiguration = ServiceConfiguration.GetRequiredAuthConfiguration(configuration);

        return new ServiceBootstrapDetails(
            "Session Operations",
            DatabaseConfigured: !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Postgres")),
            Authority: authConfiguration.Authority,
            Audience: authConfiguration.Audience,
            RabbitMqHost: configuration["RabbitMQ:Host"],
            SignalREnabled: configuration.GetValue("SignalR:Enabled", true),
            MigrationsApplyOnStartup: configuration.GetValue("Persistence:ApplyMigrationsOnStartup", false));
    }
}
