using System.Security.Claims;
using Umbral.ServiceDefaults;
using Xunit;

namespace Umbral.ServiceDefaults.Tests;

public sealed class KeycloakRoleClaimsTransformationTests
{
    private readonly KeycloakRoleClaimsTransformation transformation = new(
        new AuthConfiguration(
            "http://localhost:8080/realms/umbral",
            "mission-design-api",
            false));

    [Fact]
    public async Task TransformAsync_AddsRolesFromKeycloakRealmAndResourceAccessClaims()
    {
        var principal = CreateAuthenticatedPrincipal(new[]
        {
            new Claim("realm_access", """{"roles":["Administrator"]}"""),
            new Claim("resource_access", """{"mission-design-api":{"roles":["Operator"]},"mobile-app":{"roles":["Participant"]}}"""),
            new Claim("aud", "mission-design-api")
        });

        var transformedPrincipal = await transformation.TransformAsync(principal);

        Assert.True(transformedPrincipal.IsInRole(UmbralRoles.Administrator));
        Assert.True(transformedPrincipal.IsInRole(UmbralRoles.Operator));
        Assert.False(transformedPrincipal.IsInRole(UmbralRoles.Participant));
    }

    [Fact]
    public async Task TransformAsync_DoesNotDuplicateExistingRoles()
    {
        var principal = CreateAuthenticatedPrincipal(new[]
        {
            new Claim(ClaimTypes.Role, UmbralRoles.Operator),
            new Claim("roles", """["Operator","Participant"]""")
        });

        var transformedPrincipal = await transformation.TransformAsync(principal);
        var roles = transformedPrincipal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();

        Assert.Equal(2, roles.Length);
        Assert.Contains(UmbralRoles.Operator, roles);
        Assert.Contains(UmbralRoles.Participant, roles);
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, authenticationType: "test");
        return new ClaimsPrincipal(identity);
    }
}
