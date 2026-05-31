using MediatR;
using Umbral.IdentityAccess.Api.Infrastructure;
using Umbral.IdentityAccess.Api.Presentation;
using Umbral.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
var serviceIdentity = new ServiceIdentity("identity-access-service", "Identity and Access", "identity-access");

builder.Services.AddUmbralApiDefaults(builder.Configuration);
builder.Services.AddMediatR(typeof(Program).Assembly);
builder.Services.AddIdentityAccessInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapUmbralServiceDefaults(serviceIdentity);
var authorizedApi = app.MapUmbralAuthorizedApi(serviceIdentity);
authorizedApi.MapGet(
    "/bootstrap",
    () => Results.Ok(new
    {
        service = serviceIdentity.ServiceName,
        context = serviceIdentity.ContextName,
        capability = "operator-user-administration"
    }));
authorizedApi.MapUmbralRoleSmokeRoutes(serviceIdentity);
authorizedApi.MapOperatorRoutes();

app.Run();

public partial class Program
{
}
