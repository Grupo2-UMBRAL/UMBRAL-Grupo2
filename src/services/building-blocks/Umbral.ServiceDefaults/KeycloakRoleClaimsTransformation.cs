using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Umbral.ServiceDefaults;

public sealed class KeycloakRoleClaimsTransformation : IClaimsTransformation
{
    private static readonly StringComparer RoleComparer = StringComparer.Ordinal;
    private readonly AuthConfiguration authConfiguration;

    public KeycloakRoleClaimsTransformation(AuthConfiguration authConfiguration)
    {
        this.authConfiguration = authConfiguration ?? throw new ArgumentNullException(nameof(authConfiguration));
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        var existingRoles = new HashSet<string>(
            principal.FindAll(identity.RoleClaimType)
                .Select(claim => claim.Value),
            RoleComparer);

        var roles = GetRoles(principal, GetAllowedResourceNames(principal)).ToArray();

        foreach (var role in roles)
        {
            if (existingRoles.Add(role))
            {
                identity.AddClaim(new Claim(identity.RoleClaimType, role));
            }
        }

        return Task.FromResult(principal);
    }

    private IEnumerable<string> GetRoles(
        ClaimsPrincipal principal,
        ISet<string> allowedResourceNames)
    {
        foreach (var claim in principal.Claims.ToArray())
        {
            if (claim.Type is ClaimTypes.Role or "role")
            {
                foreach (var role in SplitClaimValues(claim.Value))
                {
                    yield return role;
                }
            }

            if (claim.Type == "roles")
            {
                foreach (var role in ParseRolesClaim(claim.Value))
                {
                    yield return role;
                }
            }

            if (claim.Type == "realm_access")
            {
                foreach (var role in ParseRolesFromNestedAccessClaim(claim.Value))
                {
                    yield return role;
                }
            }

            if (claim.Type == "resource_access")
            {
                foreach (var role in ParseRolesFromResourceAccessClaim(claim.Value, allowedResourceNames))
                {
                    yield return role;
                }
            }
        }
    }

    private HashSet<string> GetAllowedResourceNames(ClaimsPrincipal principal)
    {
        var allowedResourceNames = new HashSet<string>(StringComparer.Ordinal)
        {
            authConfiguration.Audience
        };

        foreach (var audienceClaim in principal.FindAll("aud"))
        {
            if (!string.IsNullOrWhiteSpace(audienceClaim.Value))
            {
                allowedResourceNames.Add(audienceClaim.Value);
            }
        }

        var authorizedParty = principal.FindFirst("azp")?.Value;
        if (!string.IsNullOrWhiteSpace(authorizedParty))
        {
            allowedResourceNames.Add(authorizedParty);
        }

        return allowedResourceNames;
    }

    private static IEnumerable<string> ParseRolesFromNestedAccessClaim(string claimValue)
    {
        using var document = ParseJsonDocument(claimValue);
        if (document is null)
        {
            yield break;
        }

        if (!document.RootElement.TryGetProperty("roles", out var rolesElement))
        {
            yield break;
        }

        foreach (var role in ReadRolesArray(rolesElement))
        {
            yield return role;
        }
    }

    private static IEnumerable<string> ParseRolesFromResourceAccessClaim(
        string claimValue,
        ISet<string> allowedResourceNames)
    {
        using var document = ParseJsonDocument(claimValue);
        if (document is null || document.RootElement.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var resource in document.RootElement.EnumerateObject())
        {
            if (!allowedResourceNames.Contains(resource.Name))
            {
                continue;
            }

            if (!resource.Value.TryGetProperty("roles", out var rolesElement))
            {
                continue;
            }

            foreach (var role in ReadRolesArray(rolesElement))
            {
                yield return role;
            }
        }
    }

    private static IEnumerable<string> ParseRolesClaim(string claimValue)
    {
        using var document = ParseJsonDocument(claimValue);
        if (document is not null)
        {
            foreach (var role in ReadRolesArray(document.RootElement))
            {
                yield return role;
            }

            yield break;
        }

        foreach (var role in SplitClaimValues(claimValue))
        {
            yield return role;
        }
    }

    private static IEnumerable<string> ReadRolesArray(JsonElement rolesElement)
    {
        if (rolesElement.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var roleElement in rolesElement.EnumerateArray())
        {
            if (roleElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var role = roleElement.GetString();
            if (!string.IsNullOrWhiteSpace(role))
            {
                yield return role;
            }
        }
    }

    private static JsonDocument? ParseJsonDocument(string claimValue)
    {
        if (string.IsNullOrWhiteSpace(claimValue))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(claimValue);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<string> SplitClaimValues(string claimValue)
    {
        foreach (var role in claimValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            yield return role;
        }
    }
}
