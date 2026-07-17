using System.Security.Claims;
using Xunit;

namespace Umbral.ServiceDefaults.UnitTests;

public sealed class KeycloakRoleClaimsTransformationTests
{
    private const string Audience = "umbral-mission-management-api";

    private static readonly AuthConfiguration Auth =
        new("http://localhost:8080/realms/umbral", Audience, false);

    [Fact]
    public async Task TransformAsync_UnauthenticatedIdentity_ReturnsUnchanged()
    {
        var identity = new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);
        var transformation = new KeycloakRoleClaimsTransformation(Auth);
        
        var result = await transformation.TransformAsync(principal);
        
        Assert.Same(principal, result);
    }

    [Fact]
    public async Task TransformAsync_NullPrincipal_ThrowsArgumentNullException()
    {
        var transformation = new KeycloakRoleClaimsTransformation(Auth);
        
        await Assert.ThrowsAsync<ArgumentNullException>(() => transformation.TransformAsync(null!));
    }

    [Fact]
    public async Task TransformAsync_PromotesRealmAccessRoles_ToRoleClaims()
    {
        var principal = CreatePrincipal(
            new Claim("realm_access", """{"roles":["operator","administrator"]}"""));

        var result = await Transform(principal);

        var roles = RoleValues(result);
        Assert.Contains("operator", roles);
        Assert.Contains("administrator", roles);
    }
    
    [Fact]
    public async Task TransformAsync_RealmAccess_WithoutRoles_DoesNotThrow()
    {
        var principal = CreatePrincipal(
            new Claim("realm_access", """{"not_roles":["operator"]}"""));

        var result = await Transform(principal);

        Assert.Empty(RoleValues(result));
    }

    [Fact]
    public async Task TransformAsync_PromotesResourceAccessRoles_OnlyForMatchingAudience()
    {
        var principal = CreatePrincipal(
            new Claim(
                "resource_access",
                $$"""
                {
                    "{{Audience}}": { "roles": ["operator"] },
                    "some-other-client": { "roles": ["administrator"] }
                }
                """));

        var result = await Transform(principal);

        var roles = RoleValues(result);
        Assert.Contains("operator", roles);
        Assert.DoesNotContain("administrator", roles);
    }
    
    [Fact]
    public async Task TransformAsync_ResourceAccess_WithMissingRolesArray_DoesNotThrow()
    {
        var principal = CreatePrincipal(
            new Claim(
                "resource_access",
                $$"""
                {
                    "{{Audience}}": { "no_roles": ["operator"] },
                    "some-other-client": { "roles": ["administrator"] }
                }
                """));

        var result = await Transform(principal);

        Assert.Empty(RoleValues(result));
    }
    
    [Fact]
    public async Task TransformAsync_ResourceAccess_WithNonObjectRoot_DoesNotThrow()
    {
        var principal = CreatePrincipal(
            new Claim("resource_access", """["operator"]"""));

        var result = await Transform(principal);

        Assert.Empty(RoleValues(result));
    }

    [Fact]
    public async Task TransformAsync_PromotesFlatRolesArrayClaim_ToRoleClaims()
    {
        var principal = CreatePrincipal(new Claim("roles", """["operator","participant"]"""));

        var result = await Transform(principal);

        var roles = RoleValues(result);
        Assert.Contains("operator", roles);
        Assert.Contains("participant", roles);
    }
    
    [Fact]
    public async Task TransformAsync_PromotesFlatRolesStringClaim_ToRoleClaims()
    {
        var principal = CreatePrincipal(new Claim("roles", "operator,participant"));

        var result = await Transform(principal);

        var roles = RoleValues(result);
        Assert.Contains("operator", roles);
        Assert.Contains("participant", roles);
    }

    [Fact]
    public async Task TransformAsync_PromotesSingularRoleClaim_ToRoleClaim()
    {
        var principal = CreatePrincipal(new Claim("role", "operator"));

        var result = await Transform(principal);

        Assert.Contains("operator", RoleValues(result));
    }

    [Fact]
    public async Task TransformAsync_DoesNotThrow_AndAddsNoRoles_ForMalformedJsonClaims()
    {
        var principal = CreatePrincipal(
            new Claim("realm_access", "{ not valid json"),
            new Claim("resource_access", ""),
            new Claim("roles", ""));

        var result = await Transform(principal);

        Assert.Empty(RoleValues(result));
    }

    [Fact]
    public async Task TransformAsync_DoesNotDuplicate_AlreadyPresentRoleClaim()
    {
        var principal = CreatePrincipal(
            new Claim(ClaimTypes.Role, "operator"),
            new Claim("realm_access", """{"roles":["operator"]}"""));

        var result = await Transform(principal);

        Assert.Single(RoleValues(result), role => role == "operator");
    }
    
    [Fact]
    public async Task TransformAsync_IgnoresNonStringRolesInArray()
    {
        var principal = CreatePrincipal(
            new Claim("realm_access", """{"roles":["operator", 123, true, null, ""]}"""));

        var result = await Transform(principal);

        Assert.Single(RoleValues(result), role => role == "operator");
    }
    
    [Fact]
    public async Task TransformAsync_ReadsAudienceFromAudAndAzpClaims()
    {
        var principal = CreatePrincipal(
            new Claim("aud", "custom-aud"),
            new Claim("azp", "custom-azp"),
            new Claim(
                "resource_access",
                """
                {
                    "custom-aud": { "roles": ["operator"] },
                    "custom-azp": { "roles": ["administrator"] }
                }
                """));

        var result = await Transform(principal);

        var roles = RoleValues(result);
        Assert.Contains("operator", roles);
        Assert.Contains("administrator", roles);
    }

    private static Task<ClaimsPrincipal> Transform(ClaimsPrincipal principal)
    {
        var transformation = new KeycloakRoleClaimsTransformation(Auth);
        return transformation.TransformAsync(principal);
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        // An authentication type is required so the identity reports IsAuthenticated == true,
        // which is the gate the transformation checks before promoting roles.
        var identity = new ClaimsIdentity(claims, authenticationType: "Test");
        return new ClaimsPrincipal(identity);
    }

    private static string[] RoleValues(ClaimsPrincipal principal) =>
        principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
}
