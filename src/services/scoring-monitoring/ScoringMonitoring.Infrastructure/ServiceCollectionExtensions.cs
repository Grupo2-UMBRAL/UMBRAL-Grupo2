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

        
        services.AddScoped(typeof(ScoringMonitoring.Application.Abstractions.IRepository<>), typeof(Persistence.Repository<>));
        services.AddScoped<ScoringMonitoring.Application.Abstractions.IUnitOfWork>(sp => sp.GetRequiredService<Persistence.ScoringMonitoringDbContext>());

        services.AddSingleton(_ =>
        {
            var rabbitOptions = new Messaging.RabbitMqOptions();
            configuration.GetSection("RabbitMQ").Bind(rabbitOptions);
            return rabbitOptions;
        });
        services.AddSingleton<Messaging.RabbitMqConnection>();
        services.AddHostedService<Messaging.SessionAuditEventConsumer>();
        return services;
    }
}

