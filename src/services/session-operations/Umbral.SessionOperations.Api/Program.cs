using Microsoft.AspNetCore.Authentication.JwtBearer;
using MediatR;
using Umbral.SessionOperations.Api.Application.Bootstrap.Commands;
using Umbral.SessionOperations.Api.Application.Bootstrap.Queries;
using Umbral.SessionOperations.Api.Hubs;
using Umbral.SessionOperations.Api.Infrastructure;
using Umbral.SessionOperations.Api.Presentation;
using Umbral.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
var serviceIdentity = new ServiceIdentity("session-operations-service", "Session Operations", "session-operations");

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
builder.Services.AddSessionOperationsInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapUmbralServiceDefaults(serviceIdentity);
var authorizedApi = app.MapUmbralAuthorizedApi(serviceIdentity);
authorizedApi
    .MapGet(
        "/bootstrap",
        async (ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetSessionOperationsBootstrapDetailsQuery(), cancellationToken)));
authorizedApi.MapUmbralRoleSmokeRoutes(serviceIdentity);
authorizedApi.MapLiveSessionRoutes();
authorizedApi.MapSessionEnrollmentRoutes();

if (builder.Configuration.GetValue("Persistence:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var sender = scope.ServiceProvider.GetRequiredService<ISender>();
    await sender.Send(new ApplySessionOperationsPersistenceMigrationsCommand());
}

app.MapHub<SessionOperationsHub>("/hubs/session");

app.Run();

public partial class Program
{
}
