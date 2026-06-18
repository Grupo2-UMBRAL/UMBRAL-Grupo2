namespace ScoringMonitoring.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScoringMonitoringInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddUmbralPostgresDbContext<ScoringMonitoringDbContext>(configuration, ScoringMonitoringPersistence.SchemaName);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IApplyPenaltyScoreboardStore, ApplyPenaltyScoreboardStore>();

        return services;
    }
}
