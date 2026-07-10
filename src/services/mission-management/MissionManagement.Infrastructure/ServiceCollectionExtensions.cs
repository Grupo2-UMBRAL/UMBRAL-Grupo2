using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbral.ServiceDefaults;

namespace MissionManagement.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMissionManagementInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddUmbralPostgresDbContext<MissionManagementDbContext>(configuration, MissionManagementPersistence.SchemaName);

        services.AddScoped<MissionManagement.Application.Abstractions.IMissionStore, Persistence.MissionStore>();
        return services;
    }
}

