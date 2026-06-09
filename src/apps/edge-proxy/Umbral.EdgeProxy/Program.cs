using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

builder.Services
    .AddCors(options =>
    {
        options.AddPolicy("edge", policy =>
        {
            if (allowedOrigins.Length == 0)
            {
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                return;
            }

            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCors("edge");

// Serve coverage report if available
var coveragePath = builder.Configuration["CoverageReportPath"] ?? Environment.GetEnvironmentVariable("COVERAGE_REPORT_PATH");
if (string.IsNullOrEmpty(coveragePath))
{
    var pathsToTry = new[]
    {
        "/app/coverage",
        Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../../../temp/validation/backend-coverage-report")),
        Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../../temp/validation/backend-coverage-report")),
        Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "temp/validation/backend-coverage-report"))
    };

    foreach (var path in pathsToTry)
    {
        if (Directory.Exists(path))
        {
            coveragePath = path;
            break;
        }
    }

    if (string.IsNullOrEmpty(coveragePath))
    {
        coveragePath = Path.Combine(builder.Environment.ContentRootPath, "coverage");
    }
}

// Ensure the directory exists to avoid crashes on startup
Directory.CreateDirectory(coveragePath);

app.Logger.LogInformation("Serving tests dashboard (coverage report) from path: {CoveragePath} under endpoint /coverage", coveragePath);

app.UseFileServer(new FileServerOptions
{
    FileProvider = new PhysicalFileProvider(coveragePath),
    RequestPath = "/coverage",
    EnableDefaultFiles = true
});

app.MapGet("/coverage", () => Results.Redirect("/coverage/"));

app.MapGet("/", () => Results.Ok(new
{
    service = "edge-proxy",
    role = "preferred-public-edge",
    notes = new[]
    {
        "Clients should prefer the edge-proxy base URL for local web and mobile traffic.",
        "Downstream services still validate JWT issuer and audience and enforce authorization at their own boundary."
    }
}));
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "edge-proxy" }));
app.MapReverseProxy();

app.Run();
