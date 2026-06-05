namespace Umbral.ServiceDefaults;

public sealed record ServiceBootstrapDetails(
    string Context,
    bool DatabaseConfigured,
    string Authority,
    string Audience,
    string? RabbitMqHost = null,
    bool? SignalREnabled = null,
    bool MigrationsApplyOnStartup = false,
    string ErrorMapping = "validation=400, unauthorized=401, forbidden=403, not-found=404, conflict=409, domain=422, technical=500");
