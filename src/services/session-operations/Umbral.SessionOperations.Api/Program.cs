using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Umbral.SessionOperations.Api.Hubs;
using Umbral.SessionOperations.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();
builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var auth = builder.Configuration.GetSection("Auth");
        options.Authority = auth["Authority"];
        options.RequireHttpsMetadata = auth.GetValue("RequireHttpsMetadata", false);
        options.TokenValidationParameters.ValidAudience = auth["Audience"];
        options.TokenValidationParameters.ValidateAudience = !string.IsNullOrWhiteSpace(auth["Audience"]);
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

builder.Services.AddDbContext<SessionOperationsDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres");
    options.UseNpgsql(connectionString);
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    service = "session-operations-service",
    context = "Session Operations",
    status = "bootstrapped"
}));

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "session-operations-service" }));

app.MapGroup("/api/session-operations")
    .RequireAuthorization()
    .MapGet("/bootstrap", (IConfiguration configuration) => Results.Ok(new
    {
        context = "Session Operations",
        dbConfigured = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Postgres")),
        rabbitMqHost = configuration["RabbitMQ:Host"],
        signalREnabled = configuration.GetValue("SignalR:Enabled", true)
    }));

app.MapHub<SessionOperationsHub>("/hubs/session");

app.Run();
