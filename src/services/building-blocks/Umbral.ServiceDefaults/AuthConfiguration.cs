namespace Umbral.ServiceDefaults;

public sealed record AuthConfiguration(
    string Authority,
    string Audience,
    bool RequireHttpsMetadata);
