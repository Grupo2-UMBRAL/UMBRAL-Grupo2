using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using MediatR;
using Umbral.ScoringAudit.Api.Application;
using Umbral.ScoringAudit.Api.Application.Audit;
using Umbral.ScoringAudit.Api.Application.Rankings;
using Umbral.ScoringAudit.Api.Application.Scoreboards;
using Umbral.ScoringAudit.Api.Hubs;
using Umbral.ScoringAudit.Api.Infrastructure;
using Umbral.ScoringAudit.Api.Presentation.Realtime;
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
builder.Services.AddMediatR(typeof(Program).Assembly);
builder.Services.AddScoringAuditInfrastructure(builder.Configuration);
builder.Services.AddScoped<IScoringAuditDbContext>(serviceProvider => serviceProvider.GetRequiredService<ScoringAuditDbContext>());
builder.Services.AddScoped<IScoringAuditUpdatesPublisher, SignalRScoringAuditUpdatesPublisher>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (builder.Configuration.GetValue("Persistence:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ScoringAuditDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapHub<ScoringAuditHub>("/hub/scoring");

app.Run();

public partial class Program
{
}
