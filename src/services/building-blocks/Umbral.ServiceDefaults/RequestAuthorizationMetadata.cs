namespace Umbral.ServiceDefaults;

public sealed record RequestAuthorizationMetadata(IReadOnlyCollection<string> AllowedRoles);
