using MediatR;
using Umbral.ScoringAudit.Api.Application.Bootstrap.Commands;
using Umbral.ScoringAudit.Api.Application.Bootstrap.Queries;
using Umbral.ScoringAudit.Api.Infrastructure;
using Umbral.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
var serviceIdentity = new ServiceIdentity("scoring-audit-service", "Scoring and Audit", "scoring-audit");

builder.Services.AddUmbralApiDefaults(
    builder.Configuration);
builder.Services.AddMediatR(typeof(Program).Assembly);
builder.Services.AddScoringAuditInfrastructure(builder.Configuration);

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
            Results.Ok(await sender.Send(new GetScoringAuditBootstrapDetailsQuery(), cancellationToken)));
authorizedApi.MapUmbralRoleSmokeRoutes(serviceIdentity);

if (builder.Configuration.GetValue("Persistence:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var sender = scope.ServiceProvider.GetRequiredService<ISender>();
    await sender.Send(new ApplyScoringAuditPersistenceMigrationsCommand());
}

app.Run();

public partial class Program
{
}
