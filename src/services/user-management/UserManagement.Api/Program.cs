using System.Threading.RateLimiting;
using Umbral.ServiceDefaults;
using UserManagement.Api.Controllers;
using UserManagement.Application;
using UserManagement.Application.Abstractions;
using UserManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddUmbralTelemetry(builder.Environment.ApplicationName);


builder.Services.AddControllers();
builder.Services.AddUmbralApiDefaults(builder.Configuration);
builder.Services.AddOpenApi(options =>
    options.AddUmbralDefaults("User Management API", requiresBearerAuthentication: true));

builder.Services.AddUserManagementApplication();
builder.Services.AddUserManagementInfrastructure(builder.Configuration);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Public participant self-registration is anonymous, so rate limit it per client IP to blunt abuse.
    options.AddPolicy(ParticipantSignupRateLimiter.PolicyName, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));

    // Renaming is authenticated, so it partitions by the caller rather than by IP: a shared NAT or a
    // venue's wifi would otherwise make one player's renames exhaust everyone else's budget. Its own
    // bucket, separate from signup, so neither can starve the other.
    //
    // No fallback partition key. UseRateLimiter runs after UseAuthorization below, so a request with
    // no usable token is already answered with a 401 and never reaches this; the identity is always
    // resolvable here. A "?? unknown" branch could not execute, and would advertise a defense that
    // does not exist.
    options.AddPolicy(ParticipantUsernameRateLimiter.PolicyName, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.RequestServices
                .GetRequiredService<ICurrentParticipantIdentity>()
                .GetRequiredParticipantUserId(),
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
