using System.Security.Claims;

namespace Umbral.ServiceDefaults;

public sealed class CurrentUser
{
    private readonly HashSet<string> roles;

    public CurrentUser(ClaimsPrincipal principal)
    {
        Principal = principal ?? throw new ArgumentNullException(nameof(principal));
        roles = new HashSet<string>(
            principal.FindAll(ClaimTypes.Role)
                .Select(static claim => claim.Value)
                .Where(static value => !string.IsNullOrWhiteSpace(value)),
            StringComparer.Ordinal);
    }

    public ClaimsPrincipal Principal { get; }

    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;

    public string? Subject =>
        Principal.FindFirst("sub")?.Value
        ?? Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public IReadOnlyCollection<string> Roles => roles;

    public bool IsInRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return roles.Contains(role);
    }
}
