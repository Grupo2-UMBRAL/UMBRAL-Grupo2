using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Umbral.MissionDesign.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();
builder.Services.AddAuthorization();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var auth = builder.Configuration.GetSection("Auth");
        options.Authority = auth["Authority"];
        options.RequireHttpsMetadata = auth.GetValue("RequireHttpsMetadata", false);
        options.TokenValidationParameters.ValidAudience = auth["Audience"];
        options.TokenValidationParameters.ValidateAudience = !string.IsNullOrWhiteSpace(auth["Audience"]);
    });

builder.Services.AddDbContext<MissionDesignDbContext>(options =>
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
    service = "mission-design-service",
    context = "Mission Design",
    status = "bootstrapped"
}));

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "mission-design-service" }));

app.MapGroup("/api/mission-design")
    .RequireAuthorization()
    .MapGet("/bootstrap", (IConfiguration configuration) => Results.Ok(new
    {
        context = "Mission Design",
        dbConfigured = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Postgres")),
        authority = configuration["Auth:Authority"],
        audience = configuration["Auth:Audience"]
    }));

app.Run();
