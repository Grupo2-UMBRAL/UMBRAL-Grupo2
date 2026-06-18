using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using MediatR;
using SessionOperations.Application.Hubs;
using SessionOperations.Infrastructure;
using SessionOperations.Infrastructure.Persistence;
using Umbral.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<SessionOperationsDbContext>());
builder.Services.AddSessionOperationsInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (builder.Configuration.GetValue("Persistence:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<SessionOperationsDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapHub<SessionOperationsHub>("/hubs/session");

app.Run();

public partial class Program;
