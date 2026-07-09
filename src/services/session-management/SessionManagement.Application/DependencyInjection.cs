using Microsoft.Extensions.DependencyInjection;
using SessionManagement.Application.Abstractions;
using Umbral.ServiceDefaults;

namespace SessionManagement.Application;

/// <summary>
/// Composition root for the Application layer. Registers MediatR handlers and the shared
/// cross-cutting pipeline behaviors so the API host wires everything with a single call.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddSessionManagementApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssemblyContaining<ISessionManagementDbContext>();
            configuration.AddOpenBehavior(typeof(UmbralLoggingBehavior<,>));
        });

        return services;
    }
}
