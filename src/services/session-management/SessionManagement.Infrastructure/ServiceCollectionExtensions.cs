using SessionManagement.Application.Features.EvidenceSubmissions;
using SessionManagement.Application.Realtime;
using SessionManagement.Application.Features.LiveSessions;
using SessionManagement.Application.Features.SessionLifecycle;
using SessionManagement.Application.Scoring;
using SessionManagement.Application.Features.SessionEnrollment;
using SessionManagement.Application.Abstractions;
using SessionManagement.Infrastructure.Persistence;
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

        services.AddUmbralPostgresDbContext<SessionManagementDbContext>(configuration, SessionManagementPersistence.SchemaName);
        services.AddScoped<ISessionManagementDbContext>(provider => provider.GetRequiredService<SessionManagementDbContext>());
        
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
