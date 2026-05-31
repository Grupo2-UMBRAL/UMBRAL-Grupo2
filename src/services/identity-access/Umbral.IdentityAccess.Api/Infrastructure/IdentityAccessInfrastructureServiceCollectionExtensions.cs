using Umbral.IdentityAccess.Api.Application.Operators;
using Umbral.IdentityAccess.Api.Infrastructure.Keycloak;

namespace Umbral.IdentityAccess.Api.Infrastructure;

public static class IdentityAccessInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityAccessInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<KeycloakAdminApiOptions>(
            configuration.GetSection(KeycloakAdminApiOptions.SectionName));
        services.AddHttpClient<IOperatorAdministrationPort, KeycloakAdminApiClient>();
        services.AddScoped<OperatorAdministrationService>();

        return services;
    }
}
