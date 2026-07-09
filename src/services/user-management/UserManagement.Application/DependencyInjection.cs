using System.Reflection;
using FluentValidation;
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
            // Logging is the outer behavior so it also records validation failures; validation runs
            // next so handlers only ever receive structurally valid commands.
            configuration.AddOpenBehavior(typeof(UmbralLoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(UmbralValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(applicationAssembly);

        return services;
    }
}
