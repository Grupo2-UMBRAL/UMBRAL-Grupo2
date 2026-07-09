using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using SessionManagement.Application;
using SessionManagement.Application.Hubs;
using SessionManagement.Infrastructure;
using SessionManagement.Infrastructure.Persistence;
using Umbral.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddUmbralTelemetry();

builder.Services.AddSignalR();
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

                if (!string.IsNullOrWhiteSpace(accessToken) && path.StartsWithSegments("/hubs/session"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddSessionManagementApplication();
builder.Services.AddSessionManagementInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

if (builder.Configuration.GetValue("Persistence:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<SessionManagementDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapHub<SessionManagementHub>("/hubs/session");

app.Run();

public partial class Program;
