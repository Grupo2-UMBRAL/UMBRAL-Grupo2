using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Application.EvidenceSubmissions;
using Umbral.SessionOperations.Api.Application.LiveSessions;
using Umbral.SessionOperations.Api.Application.SessionLifecycle;
using Umbral.SessionOperations.Api.Application.Scoring;
using Umbral.SessionOperations.Api.Application.SessionEnrollment;

namespace Umbral.SessionOperations.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSessionOperationsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddUmbralPostgresDbContext<SessionOperationsDbContext>(configuration, SessionOperationsPersistence.SchemaName);
        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IJoinCodeGenerator, CryptographicJoinCodeGenerator>();
        services.AddScoped<ICurrentOperatorIdentity, HttpContextCurrentOperatorIdentity>();
        services.AddScoped<ICurrentParticipantIdentity, HttpContextCurrentParticipantIdentity>();
        services.AddScoped<ICurrentOperatorIdentity, HttpContextCurrentOperatorIdentity>();
        services.AddScoped<ILiveSessionStateNotifier, SignalRLiveSessionStateNotifier>();
        services.AddTransient<AuthHeaderForwardingHandler>();
        services
            .AddHttpClient<IMissionDesignLiveSessionCatalog, MissionDesignLiveSessionCatalog>(client =>
            {
                client.BaseAddress = new Uri(
                    configuration["MissionDesign:BaseUrl"] ?? "http://mission-design-service:8080/");
            })
            .AddHttpMessageHandler<AuthHeaderForwardingHandler>();
        services
            .AddHttpClient<IScoringAuditClient, ScoringAuditHttpClient>(client =>
            {
                client.BaseAddress = new Uri(
                    configuration["ScoringAudit:BaseUrl"] ?? "http://scoring-audit-service:8080/");
            })
            .AddHttpMessageHandler<AuthHeaderForwardingHandler>();
        services.AddScoped<IServiceBootstrapDetailsProvider, SessionOperationsBootstrapDetailsProvider>();
        services.AddScoped<IServicePersistenceInitializer, SessionOperationsPersistenceInitializer>();

        return services;
    }
}
