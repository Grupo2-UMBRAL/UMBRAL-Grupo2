using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Umbral.ServiceDefaults;

namespace UserManagement.Application;

/// <summary>
/// Composition root for the Application layer. Registers MediatR handlers and the shared
/// cross-cutting pipeline behaviors so the API host wires everything with a single call.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddUserManagementApplication(this IServiceCollection services)
    {
        var applicationAssembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(applicationAssembly);
            configuration.AddOpenBehavior(typeof(UmbralLoggingBehavior<,>));
        });

        return services;
    }
}
