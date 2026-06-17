using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbral.ServiceDefaults;

namespace MissionDesign.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMissionDesignInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddUmbralPostgresDbContext<MissionDesignDbContext>(configuration, MissionDesignPersistence.SchemaName);

        return services;
    }
}
