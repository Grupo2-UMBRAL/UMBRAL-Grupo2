using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMissionDesignInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddUmbralPostgresDbContext<MissionDesignDbContext>(configuration, MissionDesignPersistence.SchemaName);
        services.AddScoped<IServiceBootstrapDetailsProvider, MissionDesignBootstrapDetailsProvider>();
        services.AddScoped<IServicePersistenceInitializer, MissionDesignPersistenceInitializer>();

        return services;
    }
}
