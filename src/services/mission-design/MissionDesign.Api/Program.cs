using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddUmbralApiDefaults(builder.Configuration);
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<IMissionDesignDbContext>());
builder.Services.AddMissionDesignInfrastructure(builder.Configuration);
builder.Services.AddScoped<IMissionDesignDbContext>(serviceProvider => serviceProvider.GetRequiredService<MissionDesignDbContext>());

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

if (builder.Configuration.GetValue("Persistence:ApplyMigrationsOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MissionDesignDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();

public partial class Program;
