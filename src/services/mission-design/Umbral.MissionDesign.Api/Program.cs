using MediatR;
using Umbral.MissionDesign.Api.Infrastructure;
using Umbral.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddUmbralApiDefaults(
    builder.Configuration);
builder.Services.AddMediatR(typeof(Program).Assembly);
builder.Services.AddUmbralPostgresDbContext<MissionDesignDbContext>(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapUmbralServiceDefaults(
    new ServiceIdentity("mission-design-service", "Mission Design", "mission-design"),
    configuration =>
    {
        var authConfiguration = ServiceConfiguration.GetRequiredAuthConfiguration(configuration);

        return new ServiceBootstrapDetails(
            "Mission Design",
            DatabaseConfigured: !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Postgres")),
            Authority: authConfiguration.Authority,
            Audience: authConfiguration.Audience);
    });

app.Run();
