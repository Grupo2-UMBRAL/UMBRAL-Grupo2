using SessionOperations.Application.Features.EvidenceSubmissions;
using SessionOperations.Application.Realtime;
using SessionOperations.Application.Features.LiveSessions;
using SessionOperations.Application.Features.SessionLifecycle;
using SessionOperations.Application.Scoring;
using SessionOperations.Application.Features.SessionEnrollment;
using SessionOperations.Application.Abstractions;
using SessionOperations.Infrastructure.Persistence;
using Umbral.ServiceDefaults;

namespace SessionOperations.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSessionOperationsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddUmbralPostgresDbContext<SessionOperationsDbContext>(configuration, SessionOperationsPersistence.SchemaName);
        services.AddScoped<ISessionOperationsDbContext>(provider => provider.GetRequiredService<SessionOperationsDbContext>());
        
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

        return services;
    }
}
