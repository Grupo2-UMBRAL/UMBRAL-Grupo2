using Umbral.ScoringAudit.Api.Application.Scoreboards;

namespace Umbral.ScoringAudit.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScoringAuditInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddUmbralPostgresDbContext<ScoringAuditDbContext>(configuration, ScoringAuditPersistence.SchemaName);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IApplyPenaltyScoreboardStore, ApplyPenaltyScoreboardStore>();

        return services;
    }
}
