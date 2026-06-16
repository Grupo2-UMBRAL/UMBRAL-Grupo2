using System.IO;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Yarp.ReverseProxy.Model;

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

var requestLogs = new ConcurrentQueue<GatewayRequestLog>();
const int MaxLogs = 100;

app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (path.Value == "/mobile")
    {
        context.Response.Redirect("/mobile/");
        return;
    }
    
    // Skip static assets and dashboard endpoints to prevent noise
    if (path.StartsWithSegments("/dashboard") ||
        path.StartsWithSegments("/coverage") ||
        path.StartsWithSegments("/health") ||
        path.StartsWithSegments("/_next") ||
        path.StartsWithSegments("/static") ||
        path == "/" ||
        path.Value?.EndsWith(".js") == true ||
        path.Value?.EndsWith(".css") == true ||
        path.Value?.EndsWith(".png") == true ||
        path.Value?.EndsWith(".jpg") == true ||
        path.Value?.EndsWith(".svg") == true ||
        path.Value?.EndsWith(".ico") == true ||
        (path.Value?.EndsWith(".json") == true && !path.StartsWithSegments("/dashboard/api")))
    {
        await next();
        return;
    }

    var stopwatch = Stopwatch.StartNew();
    var timestamp = DateTime.Now;
    string? errorMessage = null;

    try
    {
        await next();
    }
    catch (Exception ex)
    {
        errorMessage = ex.Message;
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        throw;
    }
    finally
    {
        stopwatch.Stop();

        var proxyFeature = context.Features.Get<IReverseProxyFeature>();
        var clusterId = proxyFeature?.Route?.Cluster?.ClusterId ?? "Direct/Static";
        var destination = proxyFeature?.ProxiedDestination?.Model?.Config?.Address ?? "None";

        var log = new GatewayRequestLog(
            Guid.NewGuid().ToString("N").Substring(0, 8),
            timestamp,
            context.Request.Method,
            path,
            context.Request.QueryString.ToString(),
            clusterId,
            destination,
            context.Response.StatusCode,
            stopwatch.Elapsed.TotalMilliseconds,
            errorMessage
        );

        requestLogs.Enqueue(log);
        while (requestLogs.Count > MaxLogs)
        {
            requestLogs.TryDequeue(out _);
        }
    }
});

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

// Dashboard endpoints
app.MapGet("/dashboard", () => Results.Content(GetDashboardHtml(), "text/html"));
app.MapGet("/dashboard/api/requests", () => Results.Ok(requestLogs.ToArray().OrderByDescending(l => l.Timestamp)));
app.MapPost("/dashboard/api/clear", () =>
{
    while (requestLogs.TryDequeue(out _)) { }
    return Results.Ok(new { message = "Logs cleared" });
});

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

public record GatewayRequestLog(
    string Id,
    DateTime Timestamp,
    string Method,
    string Path,
    string QueryString,
    string ClusterId,
    string DestinationAddress,
    int StatusCode,
    double DurationMs,
    string? ErrorMessage = null
);

partial class Program
{
    private static string GetDashboardHtml()
    {
        return """
        <!DOCTYPE html>
        <html lang="es">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>Gateway Dashboard - UMBRAL</title>
            <link href="https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;500;600;700&display=swap" rel="stylesheet">
            <style>
                * {
                    box-sizing: border-box;
                    margin: 0;
                    padding: 0;
                }
                body {
                    background: radial-gradient(circle at top right, #111827, #030712);
                    color: #f3f4f6;
                    font-family: 'Outfit', sans-serif;
                    min-height: 100vh;
                    padding: 2rem;
                    overflow-x: hidden;
                }
                .container {
                    max-width: 1200px;
                    margin: 0 auto;
                }
                header {
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    margin-bottom: 2rem;
                    border-bottom: 1px solid rgba(255, 255, 255, 0.05);
                    padding-bottom: 1.5rem;
                }
                .logo-section {
                    display: flex;
                    align-items: center;
                    gap: 0.75rem;
                }
                .pulse-dot {
                    width: 10px;
                    height: 10px;
                    background-color: #10b981;
                    border-radius: 50%;
                    box-shadow: 0 0 10px #10b981;
                    animation: pulse 1.5s infinite;
                }
                @keyframes pulse {
                    0% { transform: scale(0.8); box-shadow: 0 0 0 0 rgba(16, 185, 129, 0.7); }
                    70% { transform: scale(1); box-shadow: 0 0 0 6px rgba(16, 185, 129, 0); }
                    100% { transform: scale(0.8); box-shadow: 0 0 0 0 rgba(16, 185, 129, 0); }
                }
                h1 {
                    font-size: 1.5rem;
                    font-weight: 600;
                    background: linear-gradient(to right, #818cf8, #c084fc);
                    -webkit-background-clip: text;
                    -webkit-text-fill-color: transparent;
                }
                .subtitle {
                    font-size: 0.875rem;
                    color: #9ca3af;
                }
                .actions {
                    display: flex;
                    gap: 1rem;
                    align-items: center;
                }
                .btn {
                    background: rgba(255, 255, 255, 0.05);
                    border: 1px solid rgba(255, 255, 255, 0.1);
                    color: #f3f4f6;
                    padding: 0.5rem 1rem;
                    border-radius: 0.5rem;
                    cursor: pointer;
                    font-family: inherit;
                    font-size: 0.875rem;
                    font-weight: 500;
                    transition: all 0.2s ease;
                }
                .btn:hover {
                    background: rgba(255, 255, 255, 0.1);
                    border-color: rgba(255, 255, 255, 0.2);
                }
                .btn-danger {
                    background: rgba(239, 68, 68, 0.15);
                    border-color: rgba(239, 68, 68, 0.2);
                    color: #fca5a5;
                }
                .btn-danger:hover {
                    background: rgba(239, 68, 68, 0.25);
                    border-color: rgba(239, 68, 68, 0.3);
                }
                .switch-container {
                    display: flex;
                    align-items: center;
                    gap: 0.5rem;
                    font-size: 0.875rem;
                    color: #9ca3af;
                }
                /* Stats Grid */
                .stats-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
                    gap: 1.5rem;
                    margin-bottom: 2rem;
                }
                .stat-card {
                    background: rgba(22, 28, 45, 0.4);
                    backdrop-filter: blur(12px);
                    border: 1px solid rgba(255, 255, 255, 0.05);
                    border-radius: 0.75rem;
                    padding: 1.25rem;
                    transition: transform 0.2s;
                }
                .stat-card:hover {
                    transform: translateY(-2px);
                    border-color: rgba(255, 255, 255, 0.08);
                }
                .stat-title {
                    font-size: 0.875rem;
                    color: #9ca3af;
                    margin-bottom: 0.5rem;
                }
                .stat-value {
                    font-size: 1.75rem;
                    font-weight: 700;
                    color: #ffffff;
                }
                .stat-desc {
                    font-size: 0.75rem;
                    color: #6b7280;
                    margin-top: 0.25rem;
                }
                
                /* Table Container */
                .panel {
                    background: rgba(22, 28, 45, 0.3);
                    backdrop-filter: blur(12px);
                    border: 1px solid rgba(255, 255, 255, 0.05);
                    border-radius: 1rem;
                    overflow: hidden;
                }
                .panel-header {
                    padding: 1.25rem 1.5rem;
                    border-bottom: 1px solid rgba(255, 255, 255, 0.05);
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                }
                .panel-title {
                    font-size: 1.125rem;
                    font-weight: 600;
                }
                .search-input {
                    background: rgba(0, 0, 0, 0.2);
                    border: 1px solid rgba(255, 255, 255, 0.1);
                    color: #ffffff;
                    padding: 0.5rem 1rem;
                    border-radius: 0.5rem;
                    font-family: inherit;
                    font-size: 0.875rem;
                    width: 250px;
                    outline: none;
                    transition: border-color 0.2s;
                }
                .search-input:focus {
                    border-color: #818cf8;
                }
                .table-wrapper {
                    overflow-x: auto;
                    max-height: 500px;
                }
                table {
                    width: 100%;
                    border-collapse: collapse;
                    text-align: left;
                    font-size: 0.875rem;
                }
                th {
                    background: rgba(0, 0, 0, 0.2);
                    padding: 0.875rem 1.5rem;
                    font-weight: 500;
                    color: #9ca3af;
                    text-transform: uppercase;
                    font-size: 0.75rem;
                    letter-spacing: 0.05em;
                    position: sticky;
                    top: 0;
                    z-index: 10;
                }
                td {
                    padding: 1rem 1.5rem;
                    border-bottom: 1px solid rgba(255, 255, 255, 0.03);
                    color: #d1d5db;
                }
                tr:hover td {
                    background: rgba(255, 255, 255, 0.01);
                }
                
                /* Badges */
                .badge {
                    display: inline-flex;
                    align-items: center;
                    padding: 0.25rem 0.625rem;
                    border-radius: 9999px;
                    font-size: 0.75rem;
                    font-weight: 600;
                    border: 1px solid transparent;
                }
                .badge-method-get { background: rgba(16, 185, 129, 0.15); color: #34d399; border-color: rgba(16, 185, 129, 0.2); }
                .badge-method-post { background: rgba(99, 102, 241, 0.15); color: #818cf8; border-color: rgba(99, 102, 241, 0.2); }
                .badge-method-put { background: rgba(245, 158, 11, 0.15); color: #fbbf24; border-color: rgba(245, 158, 11, 0.2); }
                .badge-method-delete { background: rgba(239, 68, 68, 0.15); color: #f87171; border-color: rgba(239, 68, 68, 0.2); }
                .badge-method-other { background: rgba(107, 114, 128, 0.15); color: #9ca3af; border-color: rgba(107, 114, 128, 0.2); }
                
                .badge-status-2xx { background: rgba(16, 185, 129, 0.15); color: #34d399; border-color: rgba(16, 185, 129, 0.2); }
                .badge-status-3xx { background: rgba(59, 130, 246, 0.15); color: #60a5fa; border-color: rgba(59, 130, 246, 0.2); }
                .badge-status-4xx { background: rgba(245, 158, 11, 0.15); color: #fbbf24; border-color: rgba(245, 158, 11, 0.2); }
                .badge-status-5xx { background: rgba(239, 68, 68, 0.15); color: #f87171; border-color: rgba(239, 68, 68, 0.2); }
                
                .badge-cluster {
                    background: rgba(255, 255, 255, 0.05);
                    color: #e5e7eb;
                    border-color: rgba(255, 255, 255, 0.1);
                }
                
                .latency-normal { color: #34d399; }
                .latency-warning { color: #fbbf24; font-weight: 500; }
                .latency-danger { color: #f87171; font-weight: 600; }
                
                .empty-state {
                    padding: 3rem;
                    text-align: center;
                    color: #6b7280;
                    font-size: 0.975rem;
                }
            </style>
        </head>
        <body>
            <div class="container">
                <header>
                    <div class="logo-section">
                        <div class="pulse-dot"></div>
                        <div>
                            <h1>UMBRAL Gateway Dashboard</h1>
                            <div class="subtitle">Monitoreo del API Gateway (YARP) en tiempo real</div>
                        </div>
                    </div>
                    <div class="actions">
                        <div class="switch-container">
                            <input type="checkbox" id="auto-refresh" checked style="cursor:pointer">
                            <label for="auto-refresh" style="cursor:pointer; user-select:none">Actualización Automática</label>
                        </div>
                        <button class="btn" onclick="fetchRequests()">Actualizar Ahora</button>
                        <button class="btn btn-danger" onclick="clearLogs()">Limpiar Historial</button>
                    </div>
                </header>

                <div class="stats-grid">
                    <div class="stat-card">
                        <div class="stat-title">Peticiones Capturadas</div>
                        <div class="stat-value" id="stat-total">0</div>
                        <div class="stat-desc">Últimas 100 en memoria</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-title">Tasa de Éxito</div>
                        <div class="stat-value" id="stat-success-rate">100%</div>
                        <div class="stat-desc">Status 2xx y 3xx</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-title">Latencia Promedio</div>
                        <div class="stat-value" id="stat-latency">0 ms</div>
                        <div class="stat-desc">Tiempo de procesamiento</div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-title">Servicios Activos</div>
                        <div class="stat-value" id="stat-clusters">0</div>
                        <div class="stat-desc">Clusters enrutados</div>
                    </div>
                </div>

                <div class="panel">
                    <div class="panel-header">
                        <div class="panel-title">Historial de Tránsito de APIs</div>
                        <input type="text" id="search" class="search-input" placeholder="Filtrar por ruta..." oninput="renderTable()">
                    </div>
                    <div class="table-wrapper">
                        <table id="requests-table">
                            <thead>
                                <tr>
                                    <th style="width: 100px;">Hora</th>
                                    <th style="width: 80px;">Método</th>
                                    <th>Ruta</th>
                                    <th style="width: 150px;">Cluster Destino</th>
                                    <th>Dirección Interna</th>
                                    <th style="width: 100px;">Estado</th>
                                    <th style="width: 100px;">Duración</th>
                                </tr>
                            </thead>
                            <tbody id="table-body">
                                <!-- Carga dinámica -->
                            </tbody>
                        </table>
                        <div id="no-data" class="empty-state" style="display:none;">
                            No hay peticiones registradas.
                        </div>
                    </div>
                </div>
            </div>

            <script>
                let allRequests = [];
                let refreshInterval = null;

                function formatTime(dateStr) {
                    const date = new Date(dateStr);
                    const hrs = String(date.getHours()).padStart(2, '0');
                    const mins = String(date.getMinutes()).padStart(2, '0');
                    const secs = String(date.getSeconds()).padStart(2, '0');
                    const ms = String(date.getMilliseconds()).padStart(3, '0');
                    return `${hrs}:${mins}:${secs}.${ms}`;
                }

                function getMethodBadge(method) {
                    const m = method.toUpperCase();
                    if (m === 'GET') return `<span class="badge badge-method-get">GET</span>`;
                    if (m === 'POST') return `<span class="badge badge-method-post">POST</span>`;
                    if (m === 'PUT') return `<span class="badge badge-method-put">PUT</span>`;
                    if (m === 'DELETE') return `<span class="badge badge-method-delete">DELETE</span>`;
                    return `<span class="badge badge-method-other">${m}</span>`;
                }

                function getStatusBadge(code) {
                    let cls = 'badge-status-5xx';
                    if (code >= 200 && code < 300) cls = 'badge-status-2xx';
                    else if (code >= 300 && code < 400) cls = 'badge-status-3xx';
                    else if (code >= 400 && code < 500) cls = 'badge-status-4xx';
                    return `<span class="badge ${cls}">${code}</span>`;
                }

                function getLatencyClass(ms) {
                    if (ms < 100) return 'latency-normal';
                    if (ms < 500) return 'latency-warning';
                    return 'latency-danger';
                }

                async function fetchRequests() {
                    try {
                        const response = await fetch('/dashboard/api/requests');
                        if (response.ok) {
                            allRequests = await response.json();
                            updateStats();
                            renderTable();
                        }
                    } catch (err) {
                        console.error("Error fetching request logs:", err);
                    }
                }

                async function clearLogs() {
                    if (confirm("¿Estás seguro de que deseas vaciar el historial de logs?")) {
                        try {
                            await fetch('/dashboard/api/clear', { method: 'POST' });
                            fetchRequests();
                        } catch (err) {
                            console.error("Error clearing logs:", err);
                        }
                    }
                }

                function updateStats() {
                    const total = allRequests.length;
                    document.getElementById('stat-total').innerText = total;

                    if (total === 0) {
                        document.getElementById('stat-success-rate').innerText = '100%';
                        document.getElementById('stat-latency').innerText = '0 ms';
                        document.getElementById('stat-clusters').innerText = '0';
                        return;
                    }

                    const success = allRequests.filter(r => r.statusCode >= 200 && r.statusCode < 400).length;
                    const rate = ((success / total) * 100).toFixed(1);
                    document.getElementById('stat-success-rate').innerText = `${rate}%`;

                    const totalLatency = allRequests.reduce((sum, r) => sum + r.durationMs, 0);
                    const avgLatency = (totalLatency / total).toFixed(1);
                    document.getElementById('stat-latency').innerText = `${avgLatency} ms`;

                    const clusters = new Set(allRequests.map(r => r.clusterId).filter(c => c !== 'Direct/Static'));
                    document.getElementById('stat-clusters').innerText = clusters.size;
                }

                function renderTable() {
                    const search = document.getElementById('search').value.toLowerCase();
                    const tbody = document.getElementById('table-body');
                    const noData = document.getElementById('no-data');

                    const filtered = allRequests.filter(r => 
                        r.path.toLowerCase().includes(search) || 
                        r.clusterId.toLowerCase().includes(search) ||
                        String(r.statusCode).includes(search)
                    );

                    if (filtered.length === 0) {
                        tbody.innerHTML = '';
                        noData.style.display = 'block';
                        return;
                    }

                    noData.style.display = 'none';
                    tbody.innerHTML = filtered.map(r => `
                        <tr>
                            <td style="font-family: monospace; color: #9ca3af;">${formatTime(r.timestamp)}</td>
                            <td>${getMethodBadge(r.method)}</td>
                            <td style="font-family: monospace; font-size: 0.825rem; font-weight: 500; word-break: break-all;">${r.path}${r.queryString || ''}</td>
                            <td><span class="badge badge-cluster">${r.clusterId}</span></td>
                            <td style="font-family: monospace; font-size: 0.825rem; color: #9ca3af;">${r.destinationAddress}</td>
                            <td>${getStatusBadge(r.statusCode)}</td>
                            <td class="${getLatencyClass(r.durationMs)}" style="font-family: monospace; text-align: right; padding-right: 2rem;">${r.durationMs.toFixed(1)} ms</td>
                        </tr>
                    `).join('');
                }

                function setupRefresh() {
                    const checkbox = document.getElementById('auto-refresh');
                    
                    const startPolling = () => {
                        if (refreshInterval) clearInterval(refreshInterval);
                        refreshInterval = setInterval(fetchRequests, 2000);
                    };

                    const stopPolling = () => {
                        if (refreshInterval) {
                            clearInterval(refreshInterval);
                            refreshInterval = null;
                        }
                    };

                    checkbox.addEventListener('change', (e) => {
                        if (e.target.checked) startPolling();
                        else stopPolling();
                    });

                    if (checkbox.checked) startPolling();
                }

                // Init
                fetchRequests();
                setupRefresh();
            </script>
        </body>
        </html>
        """;
    }
}
