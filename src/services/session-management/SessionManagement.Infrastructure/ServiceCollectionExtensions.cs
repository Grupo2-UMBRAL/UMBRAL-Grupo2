using SessionManagement.Application.Features.EvidenceSubmissions;
using SessionManagement.Application.Features.LiveSessions;
using SessionManagement.Application.Features.SessionLifecycle;
using SessionManagement.Application.Abstractions.Scoring;
using SessionManagement.Application.Features.SessionEnrollment;
using SessionManagement.Application.Abstractions;
using SessionManagement.Application.Abstractions.Realtime;
using SessionManagement.Infrastructure.Persistence;
using SessionManagement.Infrastructure.Realtime;
using MassTransit;
using Umbral.ServiceDefaults;

namespace SessionManagement.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSessionManagementInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddUmbralPostgresDbContext<SessionManagementDbContext>(
            configuration,
            SessionManagementPersistence.SchemaName,
            (serviceProvider, options) =>
                options.AddInterceptors(serviceProvider.GetRequiredService<DomainEventsDispatchInterceptor>()));
        
        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IJoinCodeGenerator, CryptographicJoinCodeGenerator>();
        services.AddScoped<ICurrentOperatorIdentity, HttpContextCurrentOperatorIdentity>();
        services.AddScoped<ICurrentParticipantIdentity, HttpContextCurrentParticipantIdentity>();
        services.AddScoped<ISessionRealtimeNotifier, SignalRLiveSessionRealtimeNotifier>();
        services.AddTransient<AuthHeaderForwardingHandler>();
        services
            .AddHttpClient<IMissionManagementLiveSessionCatalog, MissionManagementLiveSessionCatalog>(client =>
            {
                client.BaseAddress = new Uri(
                    configuration["MissionManagement:BaseUrl"] ?? "http://mission-management-service:8080/");
            })
            .AddHttpMessageHandler<AuthHeaderForwardingHandler>();
        services
            .AddHttpClient<IScoringMonitoringClient, ScoringMonitoringHttpClient>(client =>
            {
                client.BaseAddress = new Uri(
                    configuration["ScoringMonitoring:BaseUrl"] ?? "http://scoring-monitoring-service:8080/");
            })
            .AddHttpMessageHandler<AuthHeaderForwardingHandler>();

        
        services.AddScoped<ILiveSessionRepository, LiveSessionRepository>();
        services.AddScoped<ILiveSessionReadRepository, LiveSessionReadRepository>();

        // Scoped so it shares the DbContext's scope with the outbox's scoped IPublishEndpoint.
        services.AddScoped<DomainEventsDispatchInterceptor>();
        services.AddSessionManagementMessaging(configuration);

        return services;
    }

    /// <summary>
    /// Wires MassTransit as the audit-event transport. In production it uses RabbitMQ with the
    /// EF Core transactional outbox (audit events are staged on <see cref="SessionManagementDbContext"/>
    /// inside the business transaction, then relayed by a background delivery service). When
    /// <c>Messaging:UseRabbitMq</c> is <c>false</c> (integration tests) it falls back to the
    /// in-memory transport with no outbox, so no broker or Postgres is required.
    /// </summary>
    private static IServiceCollection AddSessionManagementMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var useRabbitMq = configuration.GetValue("Messaging:UseRabbitMq", true);

        services.AddMassTransit(bus =>
        {
            if (useRabbitMq)
            {
                bus.AddEntityFrameworkOutbox<SessionManagementDbContext>(outbox =>
                {
                    outbox.UsePostgres();
                    outbox.UseBusOutbox();
                });

                bus.UsingRabbitMq((context, cfg) =>
                {
                    ConfigureRabbitMqHost(cfg, configuration);
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

    private static void ConfigureRabbitMqHost(IRabbitMqBusFactoryConfigurator cfg, IConfiguration configuration)
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
    }
}

