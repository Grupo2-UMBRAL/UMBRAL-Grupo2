using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using MediatR;
using ScoringMonitoring.Infrastructure;
using Umbral.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddUmbralTelemetry();

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
builder.Services.AddSignalR();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<IScoringMonitoringDbContext>());
builder.Services.AddScoringMonitoringInfrastructure(builder.Configuration);
builder.Services.AddScoped<IScoringMonitoringDbContext>(serviceProvider => serviceProvider.GetRequiredService<ScoringMonitoringDbContext>());
builder.Services.AddScoped<IScoringMonitoringUpdatesPublisher, SignalRScoringMonitoringUpdatesPublisher>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

if (builder.Configuration.GetValue("Persistence:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ScoringMonitoringDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapHub<ScoringMonitoringHub>("/hub/scoring");

app.Run();

public partial class Program;
