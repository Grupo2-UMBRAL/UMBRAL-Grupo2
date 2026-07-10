using MassTransit;
using ScoringMonitoring.Infrastructure.Messaging;

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

        services.AddScoringMonitoringMessaging(configuration);

        return services;
    }

    /// <summary>
    /// Wires MassTransit as the audit-event transport. In production it subscribes the
    /// <see cref="SessionAuditEventConsumer"/> to RabbitMQ; failed handling is retried
    /// (5 × 5s) before the message is dead-lettered — combined with the handler's EventId
    /// idempotency this keeps delivery at-least-once and safe to re-run. When
    /// <c>Messaging:UseRabbitMq</c> is <c>false</c> (integration tests) it falls back to the
    /// in-memory transport, so no broker is required.
    /// </summary>
    private static IServiceCollection AddScoringMonitoringMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var useRabbitMq = configuration.GetValue("Messaging:UseRabbitMq", true);

        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<SessionAuditEventConsumer>();

            if (useRabbitMq)
            {
                bus.UsingRabbitMq((context, cfg) =>
                {
                    var rabbit = configuration.GetSection("RabbitMQ");
                    cfg.Host(
                        rabbit.GetValue("Host", "localhost"),
                        (ushort)rabbit.GetValue("Port", 5672),
                        "/",
                        host =>
                        {
                            host.Username(rabbit.GetValue("User", "guest"));
                            host.Password(rabbit.GetValue("Password", "guest"));
                        });
                    cfg.UseMessageRetry(retry => retry.Interval(5, TimeSpan.FromSeconds(5)));
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                bus.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
            }
        });

        return services;
    }
}

