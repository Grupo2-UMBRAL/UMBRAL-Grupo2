using Microsoft.AspNetCore.Authentication.JwtBearer;
using MediatR;
using Umbral.SessionOperations.Api.Hubs;
using Umbral.SessionOperations.Api.Infrastructure;
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
builder.Services.AddMediatR(typeof(Program).Assembly);
builder.Services.AddUmbralPostgresDbContext<SessionOperationsDbContext>(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapUmbralServiceDefaults(
    new ServiceIdentity("session-operations-service", "Session Operations", "session-operations"),
    configuration =>
    {
        var authConfiguration = ServiceConfiguration.GetRequiredAuthConfiguration(configuration);

        return new ServiceBootstrapDetails(
            "Session Operations",
            DatabaseConfigured: !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Postgres")),
            Authority: authConfiguration.Authority,
            Audience: authConfiguration.Audience,
            RabbitMqHost: configuration["RabbitMQ:Host"],
            SignalREnabled: configuration.GetValue("SignalR:Enabled", true));
    });

app.MapHub<SessionOperationsHub>("/hubs/session");

app.Run();
