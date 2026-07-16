using Microsoft.EntityFrameworkCore;
using MissionManagement.Application;
using Umbral.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddUmbralTelemetry(builder.Environment.ApplicationName);

builder.Services.AddUmbralApiDefaults(builder.Configuration);
builder.Services.AddOpenApi(options =>
    options.AddUmbralDefaults("Mission Management API", requiresBearerAuthentication: true));
builder.Services.AddMissionManagementApplication();
builder.Services.AddMissionManagementInfrastructure(builder.Configuration);
builder.Services.AddScoped<IMissionManagementDbContext>(serviceProvider => serviceProvider.GetRequiredService<MissionManagementDbContext>());

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapUmbralOpenApi();

if (builder.Configuration.GetValue("Persistence:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MissionManagementDbContext>();
    await dbContext.Database.MigrateAsync();

    if (builder.Configuration.GetValue("Persistence:SeedSampleMissions", false))
    {
        await SampleMissionSeeder.SeedAsync(dbContext);
    }
}

app.Run();

public partial class Program;
