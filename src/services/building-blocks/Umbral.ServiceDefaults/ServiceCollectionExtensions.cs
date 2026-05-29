using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
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
        services.AddSingleton(authConfiguration);

        services.AddProblemDetails();
        services.AddExceptionHandler<UmbralExceptionHandler>();
        services.AddEndpointsApiExplorer();
        services.AddHealthChecks();
        services.AddSingleton<IClaimsTransformation, KeycloakRoleClaimsTransformation>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                UmbralAuthorizationPolicies.Administrator,
                policy => policy.RequireRole(UmbralRoles.Administrator));
            options.AddPolicy(
                UmbralAuthorizationPolicies.Operator,
                policy => policy.RequireRole(UmbralRoles.Operator));
            options.AddPolicy(
                UmbralAuthorizationPolicies.Participant,
                policy => policy.RequireRole(UmbralRoles.Participant));
        });
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authConfiguration.Authority;
                options.RequireHttpsMetadata = authConfiguration.RequireHttpsMetadata;
                options.TokenValidationParameters.ValidAudience = authConfiguration.Audience;
                options.TokenValidationParameters.ValidateAudience = true;
                options.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
                options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
                configureJwtBearer?.Invoke(options);
            });

        return services;
    }

    public static IServiceCollection AddUmbralPostgresDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string schemaName)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);

        var connectionString = ServiceConfiguration.GetRequiredPostgresConnectionString(configuration);
        services.AddDbContext<TContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(TContext).Assembly.FullName);
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", schemaName);
                }));

        return services;
    }
}
