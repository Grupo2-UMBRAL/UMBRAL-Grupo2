using Microsoft.Extensions.DependencyInjection;

namespace ScoringMonitoring.Application;

/// <summary>
/// Composition root for the Application layer. Registers MediatR handlers and the shared
/// cross-cutting pipeline behaviors so the API host wires everything with a single call.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddScoringMonitoringApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssemblyContaining<IScoringMonitoringDbContext>();
            configuration.AddOpenBehavior(typeof(UmbralLoggingBehavior<,>));
        });

        return services;
    }
}
