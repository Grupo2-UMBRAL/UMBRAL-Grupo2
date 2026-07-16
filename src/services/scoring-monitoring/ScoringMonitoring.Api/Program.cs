using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using ScoringMonitoring.Infrastructure;
using Umbral.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddUmbralTelemetry(builder.Environment.ApplicationName);

builder.Services.AddUmbralApiDefaults(
    builder.Configuration,
    options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrWhiteSpace(accessToken) && path.StartsWithSegments("/hub/scoring"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddOpenApi(options =>
    options.AddUmbralDefaults("Scoring and Monitoring API", requiresBearerAuthentication: true));
builder.Services.AddSignalR();
builder.Services.AddScoringMonitoringApplication();
builder.Services.AddScoringMonitoringInfrastructure(builder.Configuration);
builder.Services.AddScoped<IScoringMonitoringDbContext>(serviceProvider => serviceProvider.GetRequiredService<ScoringMonitoringDbContext>());
builder.Services.AddScoped<IScoringMonitoringUpdatesPublisher, SignalRScoringMonitoringUpdatesPublisher>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

// Liveness probe: exclude MassTransit's bus readiness check (tagged "masstransit"), which
// reports Unhealthy until the broker connection is up. Bus readiness belongs on a separate
// readiness endpoint, not on the "is the process alive" check.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = static registration => !registration.Tags.Contains("masstransit"),
});
app.MapControllers();
app.MapUmbralOpenApi();

if (builder.Configuration.GetValue("Persistence:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ScoringMonitoringDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapHub<ScoringMonitoringHub>("/hub/scoring");

app.Run();

public partial class Program;
