using UserManagement.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UserManagement.Domain.Entities;
using UserManagement.Infrastructure.Keycloak;

namespace UserManagement.Infrastructure;

public static class UserManagementInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddUserManagementInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<KeycloakAdminApiOptions>(
            configuration.GetSection(KeycloakAdminApiOptions.SectionName));
        services.AddHttpClient<IOperatorAdministrationPort, KeycloakAdminApiClient>();

        return services;
    }
}
