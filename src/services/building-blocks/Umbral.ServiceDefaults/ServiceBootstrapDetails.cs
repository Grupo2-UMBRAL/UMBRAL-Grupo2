namespace Umbral.ServiceDefaults;

public sealed record ServiceBootstrapDetails(
    string Context,
    bool DatabaseConfigured,
    string Authority,
    string Audience,
    string? RabbitMqHost = null,
    bool? SignalREnabled = null);
