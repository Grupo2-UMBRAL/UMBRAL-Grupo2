using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application;

/// <summary>
/// Composition root for the Application layer. Registers MediatR handlers and the shared
/// cross-cutting pipeline behaviors so the API host wires everything with a single call.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddMissionManagementApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssemblyContaining<IMissionManagementDbContext>();
            configuration.AddOpenBehavior(typeof(UmbralLoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(UmbralValidationBehavior<,>));
        });

        services.AddValidatorsFromAssemblyContaining<IMissionManagementDbContext>();

        return services;
    }
}
