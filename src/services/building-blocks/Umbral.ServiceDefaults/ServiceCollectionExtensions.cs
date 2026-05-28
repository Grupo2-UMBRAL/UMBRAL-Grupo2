using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Umbral.ServiceDefaults;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUmbralApiDefaults(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<JwtBearerOptions>? configureJwtBearer = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var authConfiguration = ServiceConfiguration.GetRequiredAuthConfiguration(configuration);

        services.AddProblemDetails();
        services.AddEndpointsApiExplorer();
        services.AddHealthChecks();
        services.AddAuthorization();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authConfiguration.Authority;
                options.RequireHttpsMetadata = authConfiguration.RequireHttpsMetadata;
                options.TokenValidationParameters.ValidAudience = authConfiguration.Audience;
                options.TokenValidationParameters.ValidateAudience = true;
                configureJwtBearer?.Invoke(options);
            });

        return services;
    }

    public static IServiceCollection AddUmbralPostgresDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = ServiceConfiguration.GetRequiredPostgresConnectionString(configuration);
        services.AddDbContext<TContext>(options => options.UseNpgsql(connectionString));

        return services;
    }
}
