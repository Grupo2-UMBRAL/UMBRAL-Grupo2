using UserManagement.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UserManagement.Infrastructure.Keycloak;
using UserManagement.Infrastructure.Services.Email;

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

        // One adapter, two ports: both vocabularies reach the same Keycloak admin API, and the split
        // exists so a Participant never meets an operator_* code. Registering the concrete typed
        // client and forwarding to it keeps that a naming boundary rather than a second HttpClient.
        services.AddHttpClient<KeycloakAdminApiClient>();
        services.AddTransient<IOperatorAdministrationPort>(
            provider => provider.GetRequiredService<KeycloakAdminApiClient>());
        services.AddTransient<IParticipantAdministrationPort>(
            provider => provider.GetRequiredService<KeycloakAdminApiClient>());

        services.AddScoped<ICurrentParticipantIdentity, HttpContextCurrentParticipantIdentity>();

        services.Configure<SmtpEmailOptions>(
            configuration.GetSection(SmtpEmailOptions.SectionName));
        services.AddScoped<IEmailNotificationService, SmtpEmailNotificationService>();

        return services;
    }
}
