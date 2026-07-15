using System;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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

// Developer hub: serve the coverlet/ReportGenerator HTML coverage report through the edge
// when the report directory is available. Local-only convenience for the academic delivery;
// the report is produced by scripts/Publish-BackendCoverageReports.ps1.
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

// Root of the edge is the local "developer hub": a single page that centralizes links to
// every runtime resource of the project (service APIs, identity, messaging, observability
// and the coverage report), matching the delivery goal of one entry point for the stack.
app.MapGet("/", (IConfiguration config) =>
    Results.Content(EdgeHubPage.Build(config, coverageAvailable), "text/html; charset=utf-8"));

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
app.MapReverseProxy();

app.Run();

internal static class EdgeHubPage
{
    public static string Build(IConfiguration config, bool coverageAvailable)
    {
        var rabbitUrl = config["Hub:RabbitMqManagementUrl"] ?? "http://localhost:16672";
        var aspireUrl = config["Hub:AspireDashboardUrl"] ?? "http://localhost:19888";
        var webUrl = config["Hub:WebConsoleUrl"] ?? "http://localhost:3000";

        var sb = new StringBuilder();
        sb.Append("""
<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>UMBRAL · Edge Hub</title>
<style>
  :root{color-scheme:light dark;--bg:#0f1117;--card:#191c26;--fg:#e8eaf2;--muted:#9aa2b5;--line:#2a2f3d;--accent:#7c5cff;}
  @media (prefers-color-scheme:light){:root{--bg:#f5f6fa;--card:#fff;--fg:#1b1e28;--muted:#5b6376;--line:#e4e7ef;--accent:#5b3df5;}}
  *{box-sizing:border-box}
  body{margin:0;font:16px/1.5 system-ui,Segoe UI,Roboto,sans-serif;background:var(--bg);color:var(--fg);}
  header{padding:40px 24px 8px;max-width:1040px;margin:0 auto;}
  h1{margin:0;font-size:26px;letter-spacing:.5px}
  h1 span{color:var(--accent)}
  p.lead{color:var(--muted);margin:6px 0 0}
  main{max-width:1040px;margin:0 auto;padding:24px;display:grid;gap:20px;grid-template-columns:repeat(auto-fill,minmax(300px,1fr))}
  section{background:var(--card);border:1px solid var(--line);border-radius:14px;padding:18px 20px}
  section h2{margin:0 0 4px;font-size:13px;text-transform:uppercase;letter-spacing:1px;color:var(--muted)}
  ul{list-style:none;margin:10px 0 0;padding:0}
  li{margin:0}
  a.link{display:flex;align-items:center;justify-content:space-between;gap:10px;padding:9px 12px;border-radius:9px;color:var(--fg);text-decoration:none;border:1px solid transparent}
  a.link:hover{background:rgba(124,92,255,.10);border-color:var(--line)}
  a.link .n{font-weight:600}
  a.link .d{font-size:12px;color:var(--muted)}
  .badge{font-size:11px;padding:2px 8px;border-radius:999px;border:1px solid var(--line);color:var(--muted)}
  .badge.off{opacity:.6}
  footer{max-width:1040px;margin:0 auto;padding:8px 24px 48px;color:var(--muted);font-size:13px}
  code{background:rgba(124,92,255,.12);padding:1px 6px;border-radius:6px;font-size:13px}
</style>
</head>
<body>
<header>
  <h1>UMBRAL <span>·</span> Edge Hub</h1>
  <p class="lead">Punto de entrada local unico del stack. Enruta clientes y centraliza los recursos operativos del proyecto.</p>
</header>
<main>
""");

        Section(sb, "APIs de servicio (Swagger vía edge)", new[]
        {
            Link("/user-management/swagger", "User Management", "Identidad / operadores / participantes", true),
            Link("/mission-management/swagger", "Mission Management", "Autoría de misiones y etapas", true),
            Link("/session-management/swagger", "Session Management", "Sesión en vivo y evidencias", true),
            Link("/scoring-monitoring/swagger", "Scoring & Monitoring", "Puntaje, ranking y auditoría", true),
        });

        Section(sb, "Salud", new[]
        {
            Link("/health", "Edge health", "Liveness del edge-proxy", true),
            Link("/user-management/health", "User health", null, true),
            Link("/mission-management/health", "Mission health", null, true),
            Link("/session-management/health", "Session health", null, true),
            Link("/scoring-monitoring/health", "Scoring health", null, true),
        });

        Section(sb, "Tiempo real", new[]
        {
            Link("/session-hub", "SignalR · session-hub", "WebSocket de sesión en vivo", true),
        });

        Section(sb, "Identidad", new[]
        {
            Link("/auth/admin/", "Keycloak Admin", "Realm umbral · roles y usuarios", true),
            Link("/auth/realms/umbral/.well-known/openid-configuration", "OIDC discovery", "Metadata del realm", true),
        });

        Section(sb, "Mensajería", new[]
        {
            Link(rabbitUrl, "RabbitMQ Management", "Colas y exchanges (MassTransit outbox)", true),
        });

        Section(sb, "Observabilidad", new[]
        {
            Link(aspireUrl, "Aspire Dashboard", "Trazas, métricas y logs (OTEL)", true),
        });

        Section(sb, "Calidad", new[]
        {
            Link("/coverage/", "Reporte de cobertura", "coverlet + ReportGenerator (HTML)", coverageAvailable),
        });

        Section(sb, "Aplicaciones", new[]
        {
            Link(webUrl, "Consola Web", "Admin + Operador", true),
            Link("/mobile", "Cliente Mobile (web)", "Participante · Expo", true),
        });

        sb.Append("</main>\n<footer>");
        if (!coverageAvailable)
        {
            sb.Append("El reporte de cobertura aparece deshabilitado: genéralo con <code>scripts/Publish-BackendCoverageReports.ps1</code> y monta la carpeta en el edge. ");
        }
        sb.Append("Recursos solo para entorno local de desarrollo. Metadata del edge en <code>/edge-info</code>.");
        sb.Append("</footer>\n</body>\n</html>");
        return sb.ToString();
    }

    private static void Section(StringBuilder sb, string title, string[] links)
    {
        sb.Append("<section><h2>").Append(Encode(title)).Append("</h2><ul>");
        foreach (var link in links)
        {
            sb.Append(link);
        }
        sb.Append("</ul></section>\n");
    }

    private static string Link(string href, string name, string? description, bool enabled)
    {
        var badge = enabled
            ? string.Empty
            : "<span class=\"badge off\">no disponible</span>";
        var target = href.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty;
        var desc = string.IsNullOrEmpty(description) ? string.Empty : $"<span class=\"d\">{Encode(description!)}</span>";
        var attrs = enabled ? $"href=\"{Encode(href)}\"{target}" : "href=\"#\" aria-disabled=\"true\"";
        return $"<li><a class=\"link\" {attrs}><span><span class=\"n\">{Encode(name)}</span> {desc}</span>{badge}</a></li>";
    }

    private static string Encode(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
