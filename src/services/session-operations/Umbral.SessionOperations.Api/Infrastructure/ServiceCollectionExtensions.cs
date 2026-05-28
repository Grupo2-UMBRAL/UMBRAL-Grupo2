using Umbral.ServiceDefaults;

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
        services.AddScoped<IServiceBootstrapDetailsProvider, SessionOperationsBootstrapDetailsProvider>();
        services.AddScoped<IServicePersistenceInitializer, SessionOperationsPersistenceInitializer>();

        return services;
    }
}
