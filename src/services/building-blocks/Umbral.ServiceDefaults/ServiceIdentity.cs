namespace Umbral.ServiceDefaults;

public sealed record ServiceIdentity(
    string ServiceName,
    string ContextName,
    string ApiRouteSegment);
