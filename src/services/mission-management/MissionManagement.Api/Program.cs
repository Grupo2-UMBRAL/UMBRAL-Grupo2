using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddUmbralTelemetry();

builder.Services.AddUmbralApiDefaults(builder.Configuration);
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<IMissionManagementDbContext>());
builder.Services.AddMissionManagementInfrastructure(builder.Configuration);
builder.Services.AddScoped<IMissionManagementDbContext>(serviceProvider => serviceProvider.GetRequiredService<MissionManagementDbContext>());

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

if (builder.Configuration.GetValue("Persistence:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MissionManagementDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();

public partial class Program;
