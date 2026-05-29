using MediatR;
using Umbral.MissionDesign.Api.Application.Bootstrap.Commands;
using Umbral.MissionDesign.Api.Application.Bootstrap.Queries;
using Umbral.MissionDesign.Api.Infrastructure;
using Umbral.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
var serviceIdentity = new ServiceIdentity("mission-design-service", "Mission Design", "mission-design");

builder.Services.AddUmbralApiDefaults(
    builder.Configuration);
builder.Services.AddMediatR(typeof(Program).Assembly);
builder.Services.AddMissionDesignInfrastructure(builder.Configuration);

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
            Results.Ok(await sender.Send(new GetMissionDesignBootstrapDetailsQuery(), cancellationToken)));
authorizedApi.MapUmbralRoleSmokeRoutes(serviceIdentity);

if (builder.Configuration.GetValue("Persistence:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var sender = scope.ServiceProvider.GetRequiredService<ISender>();
    await sender.Send(new ApplyMissionDesignPersistenceMigrationsCommand());
}

app.Run();

public partial class Program
{
}
