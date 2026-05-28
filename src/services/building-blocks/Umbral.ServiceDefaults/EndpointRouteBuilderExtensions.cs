using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace Umbral.ServiceDefaults;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapUmbralServiceDefaults(
        this IEndpointRouteBuilder endpoints,
        ServiceIdentity serviceIdentity,
        Func<IConfiguration, ServiceBootstrapDetails> bootstrapDetailsFactory)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(serviceIdentity);
        ArgumentNullException.ThrowIfNull(bootstrapDetailsFactory);

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

        endpoints.MapGroup($"/api/{serviceIdentity.ApiRouteSegment}")
            .RequireAuthorization()
            .MapGet("/bootstrap", (IConfiguration configuration) => Results.Ok(bootstrapDetailsFactory(configuration)));

        return endpoints;
    }
}
