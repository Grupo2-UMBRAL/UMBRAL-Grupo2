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
}
