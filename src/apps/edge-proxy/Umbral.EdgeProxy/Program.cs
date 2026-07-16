using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Umbral.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddUmbralTelemetry();

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

// The Azure Container Apps ingress terminates TLS and forwards plain http to this container, so
// without this the edge sees Request.Scheme == "http" and YARP's "X-Forwarded: Set" transform
// overwrites the ingress's "X-Forwarded-Proto: https" with it. Downstream services build their
// OpenAPI servers[] from that header, so the deployed reference would advertise http:// URLs and
// the browser would refuse them as mixed content. Must run before anything reads the scheme.
// The ingress is not loopback, so the default known-proxy allowlist would ignore it.
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

// Coverage report (coverlet/ReportGenerator HTML) served through the edge when the report folder
// is mounted; it is produced by scripts/Publish-BackendCoverageReports.ps1. No environment check
// is needed: Hub:CoverageReportPath is only set by docker-compose, so a deployed edge falls back
// to the empty default and serves nothing.
// Must run BEFORE routing: otherwise the YARP catch-all endpoint is selected first and the
// static-file middleware short-circuits (skips when an endpoint delegate is already set).
var coverageReportPath = builder.Configuration["Hub:CoverageReportPath"];
var coverageAvailable = !string.IsNullOrWhiteSpace(coverageReportPath)
    && Directory.Exists(coverageReportPath);
if (coverageAvailable)
{
    app.UseFileServer(new FileServerOptions
    {
        FileProvider = new PhysicalFileProvider(Path.GetFullPath(coverageReportPath!)),
        RequestPath = "/coverage",
        EnableDefaultFiles = true,
        EnableDirectoryBrowsing = false,
    });
}

app.UseRouting();
app.UseCors("edge");

// The API reference is the directory of the stack, so the root just points at it.
app.MapGet("/", () => Results.Redirect("/swagger"));

// Machine-readable edge metadata (kept for programmatic consumers / smoke checks).
app.MapGet("/edge-info", () => Results.Ok(new
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

// One reference page for the four services, served from the edge so the deployed demo has a single
// URL. The edge publishes no document of its own: its only endpoints are "/", "/edge-info" and
// "/health", and the proxied routes never reach ApiExplorer. Each document is fetched back through
// this same host, which keeps it same-origin and sidesteps CORS.
app.MapUmbralApiReferenceHub(
    ("user-management", "User Management"),
    ("mission-management", "Mission Management"),
    ("session-management", "Session Management"),
    ("scoring-monitoring", "Scoring & Monitoring"));

app.MapReverseProxy();

app.Run();
