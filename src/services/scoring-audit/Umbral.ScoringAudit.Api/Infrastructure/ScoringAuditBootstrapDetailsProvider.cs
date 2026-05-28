using Umbral.ServiceDefaults;

namespace Umbral.ScoringAudit.Api.Infrastructure;

public sealed class ScoringAuditBootstrapDetailsProvider(IConfiguration configuration) : IServiceBootstrapDetailsProvider
{
    public ServiceBootstrapDetails GetBootstrapDetails()
    {
        var authConfiguration = ServiceConfiguration.GetRequiredAuthConfiguration(configuration);

        return new ServiceBootstrapDetails(
            "Scoring and Audit",
            DatabaseConfigured: !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Postgres")),
            Authority: authConfiguration.Authority,
            Audience: authConfiguration.Audience,
            RabbitMqHost: configuration["RabbitMQ:Host"],
            MigrationsApplyOnStartup: configuration.GetValue("Persistence:ApplyMigrationsOnStartup", false));
    }
}
