using MediatR;
using Umbral.ScoringAudit.Api.Infrastructure;
using Umbral.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddUmbralApiDefaults(
    builder.Configuration);
builder.Services.AddMediatR(typeof(Program).Assembly);
builder.Services.AddUmbralPostgresDbContext<ScoringAuditDbContext>(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapUmbralServiceDefaults(
    new ServiceIdentity("scoring-audit-service", "Scoring and Audit", "scoring-audit"),
    configuration =>
    {
        var authConfiguration = ServiceConfiguration.GetRequiredAuthConfiguration(configuration);

        return new ServiceBootstrapDetails(
            "Scoring and Audit",
            DatabaseConfigured: !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Postgres")),
            Authority: authConfiguration.Authority,
            Audience: authConfiguration.Audience,
            RabbitMqHost: configuration["RabbitMQ:Host"]);
    });

app.Run();
