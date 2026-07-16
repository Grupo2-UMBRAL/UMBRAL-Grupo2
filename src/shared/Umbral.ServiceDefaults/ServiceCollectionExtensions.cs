using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
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

    /// <summary>
    /// Wires OTLP logs, metrics and traces for one service, and formats its console logs as JSON.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceName">
    /// Value for the <c>service.name</c> resource attribute. Pass <c>builder.Environment.ApplicationName</c>
    /// so it cannot drift from the assembly it describes.
    /// </param>
    /// <remarks>
    /// Naming is not cosmetic. Every container runs <c>dotnet &lt;Service&gt;.Api.dll</c>, so the process
    /// name is "dotnet" in all of them; with no explicit service.name the SDK falls back to
    /// <c>unknown_service:dotnet</c> and all five services collapse into a single indistinguishable
    /// resource in the dashboard, which makes their telemetry useless for debugging.
    /// All three signals go through the same builder so they share one <c>ConfigureResource</c>. Logs
    /// registered the old way (<c>ILoggingBuilder.AddOpenTelemetry</c>) do not inherit that resource and
    /// keep reporting <c>unknown_service:dotnet</c> while traces and metrics report the real name.
    /// The console stays the deployed log path: Container Apps ships stdout to Log Analytics. It is
    /// formatted as JSON because the default formatter writes a multi-line payload for multi-line
    /// messages (EF Core SQL above all), and the runtime captures stdout one line at a time, so a
    /// single entry lands as several unrelated records that no query can stitch back together.
    /// </remarks>
    public static IServiceCollection AddUmbralTelemetry(this IServiceCollection services, string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        services.AddLogging(logging => logging.AddJsonConsole(options => options.IncludeScopes = true));

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithLogging(
                logging => logging.AddOtlpExporter(),
                options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                })
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
