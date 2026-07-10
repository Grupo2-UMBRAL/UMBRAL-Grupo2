using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

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
        services.AddSingleton<IClaimsTransformation, KeycloakRoleClaimsTransformation>();

        services.AddProblemDetails();
        services.AddExceptionHandler<UmbralExceptionHandler>();
        services.AddEndpointsApiExplorer();
        services.AddHttpContextAccessor();
        services.AddHealthChecks();
        services.AddControllers();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                UmbralRoles.Administrator,
                policy => policy.RequireRole(UmbralRoles.Administrator));
            options.AddPolicy(
                UmbralRoles.Operator,
                policy => policy.RequireRole(UmbralRoles.Operator));
            options.AddPolicy(
                UmbralRoles.Participant,
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
        string schemaName,
        Action<IServiceProvider, DbContextOptionsBuilder>? configureOptions = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);

        var connectionString = ServiceConfiguration.GetRequiredPostgresConnectionString(configuration);
        services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(TContext).Assembly.FullName);
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", schemaName);
                });
            configureOptions?.Invoke(serviceProvider, options);
        });

        return services;
    }

    public static IServiceCollection AddUmbralTelemetry(this IServiceCollection services)
    {
        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.AddOtlpExporter();
            });
        });

        services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddRuntimeInstrumentation();
                
                metrics.AddOtlpExporter();
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation();

                tracing.AddOtlpExporter();
            });

        return services;
    }
}
