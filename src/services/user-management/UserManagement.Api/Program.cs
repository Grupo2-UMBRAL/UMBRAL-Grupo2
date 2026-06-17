using Umbral.ServiceDefaults;
using UserManagement.Infrastructure;
using UserManagement.Application.Features.Operators.Commands.CreateOperator;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddUmbralApiDefaults(builder.Configuration);

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateOperatorCommandHandler).Assembly));
builder.Services.AddUserManagementInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();



app.MapControllers();

app.Run();
