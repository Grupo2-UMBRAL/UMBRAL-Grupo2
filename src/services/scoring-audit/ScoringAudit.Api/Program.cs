using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using MediatR;
using ScoringAudit.Infrastructure;
using ScoringAudit.Infrastructure.Realtime;
using Umbral.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<IScoringAuditDbContext>());
builder.Services.AddScoringAuditInfrastructure(builder.Configuration);
builder.Services.AddScoped<IScoringAuditDbContext>(serviceProvider => serviceProvider.GetRequiredService<ScoringAuditDbContext>());
builder.Services.AddScoped<IScoringAuditUpdatesPublisher, SignalRScoringAuditUpdatesPublisher>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

if (builder.Configuration.GetValue("Persistence:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ScoringAuditDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapHub<ScoringAuditHub>("/hub/scoring");

app.Run();

public partial class Program;
