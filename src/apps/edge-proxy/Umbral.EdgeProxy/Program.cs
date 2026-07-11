using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

app.UseCors("edge");

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
