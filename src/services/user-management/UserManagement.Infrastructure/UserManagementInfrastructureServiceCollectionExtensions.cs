using UserManagement.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UserManagement.Domain.Entities;
using UserManagement.Infrastructure.Keycloak;
using UserManagement.Infrastructure.Services.Email;

namespace UserManagement.Infrastructure;

public static class IdentityAccessInfrastructureServiceCollectionExtensions
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

        services.Configure<SmtpEmailOptions>(
            configuration.GetSection(SmtpEmailOptions.SectionName));
        services.AddScoped<IEmailNotificationService, SmtpEmailNotificationService>();

        return services;
    }
}
