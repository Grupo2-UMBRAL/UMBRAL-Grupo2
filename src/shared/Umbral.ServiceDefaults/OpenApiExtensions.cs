using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Umbral.ServiceDefaults;

public static class OpenApiExtensions
{
    /// <summary>
    /// Applies the shared UMBRAL configuration to an OpenAPI document.
    /// </summary>
    /// <remarks>
    /// Call <c>AddOpenApi</c> from the .Api project itself and pass this in:
    /// <c>builder.Services.AddOpenApi(o => o.AddUmbralDefaults("Title", true));</c>
    /// The XML doc-comment source generator only intercepts <c>AddOpenApi</c> call sites in the
    /// project being compiled, and only finds the XML files when Microsoft.AspNetCore.OpenApi is a
    /// direct PackageReference there. Wrapping the call inside this assembly compiles and runs, but
    /// silently drops every description from the document.
    /// </remarks>
    public static OpenApiOptions AddUmbralDefaults(
        this OpenApiOptions options,
        string title,
        bool requiresBearerAuthentication)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        options.AddDocumentTransformer((document, context, _) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = title,
                Version = "v1"
            };

            ApplyForwardedServer(document, context);

            if (requiresBearerAuthentication)
            {
                AddBearerSecurityScheme(document);
            }

            return Task.CompletedTask;
        });

        if (requiresBearerAuthentication)
        {
            options.AddOperationTransformer((operation, context, _) =>
            {
                var endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;
                if (endpointMetadata?.OfType<IAllowAnonymous>().Any() == true)
                {
                    return Task.CompletedTask;
                }

                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
                });

                return Task.CompletedTask;
            });
        }

        return options;
    }

    public static WebApplication MapUmbralOpenApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapOpenApi();
        app.MapScalarApiReference("/swagger");

        return app;
    }

    /// <summary>
    /// Serves one Scalar page listing every service document proxied through this host, so the
    /// deployed demo has a single entry point instead of four.
    /// </summary>
    public static WebApplication MapUmbralApiReferenceHub(
        this WebApplication app,
        params (string Slug, string Title)[] services)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(services);

        app.MapScalarApiReference("/swagger", options =>
        {
            foreach (var (slug, title) in services)
            {
                options.AddDocument(slug, title, $"/{slug}/openapi/v1.json");
            }
        });

        return app;
    }

    // A service builds its document from the request it is serving, so behind the edge it would
    // advertise the internal container address (http://user-management-service:8080), which the
    // browser cannot reach. YARP strips the route prefix and does not put it in
    // X-Forwarded-Prefix -- that header carries the original PathBase, and the edge has none -- so
    // the prefix is set explicitly per route in the edge's appsettings.
    //
    // Paths stay untouched: OpenAPI resolves a call as server + path, so
    // http://localhost:7500/user-management + /api/operators is the real edge route.
    //
    // No prefix header means the document was fetched straight from the service, where the
    // framework default is already right.
    //
    // These headers are caller-controlled. Here that only changes the URL the reference page
    // displays -- it drives no routing and no authorization. Restricting to an allowlist of known
    // hosts is the mitigation if this outlives its academic scope.
    private static void ApplyForwardedServer(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context)
    {
        var request = context.ApplicationServices
            .GetService<IHttpContextAccessor>()?
            .HttpContext?
            .Request;

        if (request is null)
        {
            return;
        }

        var prefix = request.Headers["X-Forwarded-Prefix"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return;
        }

        var scheme = request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? request.Scheme;
        var host = request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? request.Host.Value;

        document.Servers =
        [
            new OpenApiServer { Url = $"{scheme}://{host}/{prefix.Trim('/')}" }
        ];
    }

    private static void AddBearerSecurityScheme(OpenApiDocument document)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            In = ParameterLocation.Header,
            BearerFormat = "JWT"
        };
    }
}
