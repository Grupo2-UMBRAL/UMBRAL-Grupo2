using System.Threading.RateLimiting;
using Umbral.ServiceDefaults;
using UserManagement.Api.Controllers;
using UserManagement.Application;
using UserManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddUmbralTelemetry();


builder.Services.AddControllers();
builder.Services.AddUmbralApiDefaults(builder.Configuration);
builder.Services.AddOpenApi(options =>
    options.AddUmbralDefaults("User Management API", requiresBearerAuthentication: true));

builder.Services.AddUserManagementApplication();
builder.Services.AddUserManagementInfrastructure(builder.Configuration);

// Public participant self-registration is anonymous, so rate limit it per client IP to blunt abuse.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(ParticipantSignupRateLimiter.PolicyName, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapUmbralOpenApi();

app.Run();
