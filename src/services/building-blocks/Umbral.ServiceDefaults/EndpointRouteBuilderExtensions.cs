using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Umbral.ServiceDefaults;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapUmbralServiceDefaults(
        this IEndpointRouteBuilder endpoints,
        ServiceIdentity serviceIdentity)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(serviceIdentity);

        endpoints.MapGet("/", () => Results.Ok(new
        {
            service = serviceIdentity.ServiceName,
            context = serviceIdentity.ContextName,
            status = "bootstrapped"
        }));

        endpoints.MapGet("/health", () => Results.Ok(new
        {
            status = "healthy",
            service = serviceIdentity.ServiceName
        }));

        return endpoints;
    }

    public static RouteGroupBuilder MapUmbralAuthorizedApi(
        this IEndpointRouteBuilder endpoints,
        ServiceIdentity serviceIdentity)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(serviceIdentity);

        return endpoints.MapGroup($"/api/{serviceIdentity.ApiRouteSegment}")
            .RequireAuthorization();
    }

    public static RouteGroupBuilder MapUmbralRoleSmokeRoutes(
        this RouteGroupBuilder authorizedApi,
        ServiceIdentity serviceIdentity)
    {
        ArgumentNullException.ThrowIfNull(authorizedApi);
        ArgumentNullException.ThrowIfNull(serviceIdentity);

        MapRoleSmokeRoute(
            authorizedApi,
            serviceIdentity,
            "administrator",
            UmbralAuthorizationPolicies.Administrator,
            UmbralRoles.Administrator);
        MapRoleSmokeRoute(
            authorizedApi,
            serviceIdentity,
            "operator",
            UmbralAuthorizationPolicies.Operator,
            UmbralRoles.Operator);
        MapRoleSmokeRoute(
            authorizedApi,
            serviceIdentity,
            "participant",
            UmbralAuthorizationPolicies.Participant,
            UmbralRoles.Participant);

        return authorizedApi;
    }

    private static void MapRoleSmokeRoute(
        IEndpointRouteBuilder authorizedApi,
        ServiceIdentity serviceIdentity,
        string routeSegment,
        string policyName,
        string role)
    {
        authorizedApi.MapGet($"/smoke/{routeSegment}", () => Results.Ok(new
            {
                service = serviceIdentity.ServiceName,
                context = serviceIdentity.ContextName,
                requiredRole = role,
                status = "authorized"
            }))
            .RequireAuthorization(policyName);
    }
}
