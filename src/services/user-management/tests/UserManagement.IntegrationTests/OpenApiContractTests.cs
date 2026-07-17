using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace UserManagement.IntegrationTests;

public sealed class OpenApiContractTests
{
    [Fact]
    public async Task Api_PublishesOpenApiDocumentWithoutInteractiveReference()
    {
        await using var factory = new UserManagementApiFactory();
        var client = factory.CreateClient();

        var documentResponse = await client.GetAsync("/openapi/v1.json");
        var referenceResponse = await client.GetAsync("/swagger");

        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        Assert.Equal("application/json", documentResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, referenceResponse.StatusCode);

        using var document = JsonDocument.Parse(await documentResponse.Content.ReadAsStreamAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/operators", out _));
        Assert.False(paths.TryGetProperty("/health", out _));
        Assert.True(document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .TryGetProperty("OAuth2", out var oauth2));
        Assert.Equal("oauth2", oauth2.GetProperty("type").GetString());

        var authorizationCode = oauth2
            .GetProperty("flows")
            .GetProperty("authorizationCode");

        Assert.Equal("/auth/realms/umbral/protocol/openid-connect/auth",
            authorizationCode.GetProperty("authorizationUrl").GetString());
        Assert.Equal("/auth/realms/umbral/protocol/openid-connect/token",
            authorizationCode.GetProperty("tokenUrl").GetString());
        Assert.True(authorizationCode.GetProperty("scopes").TryGetProperty("openid", out _));
    }

    [Fact]
    public async Task OpenApiDocument_DescribesOperatorCollectionOperation()
    {
        await using var factory = new UserManagementApiFactory();
        var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStreamAsync("/openapi/v1.json"));
        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/operators")
            .GetProperty("get");

        Assert.Equal("List operators", operation.GetProperty("summary").GetString());
        Assert.True(operation.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(operation.GetProperty("responses").TryGetProperty("401", out _));
        Assert.True(operation.GetProperty("responses").TryGetProperty("403", out _));
    }

    // These actions returned Task<IActionResult>, which erases the response type: the 200 carried no
    // schema at all, so the reference page showed "OK" and nothing else. ActionResult<T> lets
    // ApiExplorer infer it from the signature, with the compiler keeping the two in step.
    [Fact]
    public async Task OpenApiDocument_DescribesOperatorResponseSchema()
    {
        await using var factory = new UserManagementApiFactory();
        var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStreamAsync("/openapi/v1.json"));
        var content = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/operators")
            .GetProperty("get")
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content");

        var items = content.GetProperty("application/json").GetProperty("schema").GetProperty("items");

        Assert.Equal("#/components/schemas/OperatorDto", items.GetProperty("$ref").GetString());
    }

    [Fact]
    public async Task OpenApiDocument_DoesNotRequireBearerForParticipantRegistration()
    {
        await using var factory = new UserManagementApiFactory();
        var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStreamAsync("/openapi/v1.json"));
        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/participants")
            .GetProperty("post");

        Assert.False(operation.TryGetProperty("security", out _));
    }

    // Served straight from the service, the framework's own server URL is already correct.
    [Fact]
    public async Task OpenApiDocument_AdvertisesRequestedHostWhenNotProxied()
    {
        await using var factory = new UserManagementApiFactory();
        var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStreamAsync("/openapi/v1.json"));
        var server = document.RootElement.GetProperty("servers")[0].GetProperty("url").GetString();

        Assert.Equal("http://localhost/", server);
    }

    // Behind the edge the service would otherwise advertise its internal container address, which no
    // browser can reach, breaking every Send Request in the published demo. The edge sets
    // X-Forwarded-Prefix explicitly per route: YARP derives that header from PathBase, which the edge
    // does not have, and its default X-Forwarded transform deletes any inherited value as an
    // anti-spoofing measure -- hence "Prefix": "Off" alongside it in the edge configuration.
    [Fact]
    public async Task OpenApiDocument_AdvertisesForwardedServerWhenProxied()
    {
        await using var factory = new UserManagementApiFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/openapi/v1.json");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", "umbral.example.org");
        request.Headers.Add("X-Forwarded-Prefix", "/user-management");

        var response = await client.SendAsync(request);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var server = document.RootElement.GetProperty("servers")[0].GetProperty("url").GetString();

        Assert.Equal("https://umbral.example.org/user-management", server);

        // The prefix rides on the server URL, never on the paths: OpenAPI resolves a call as
        // server + path, so duplicating it in both would double it.
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/operators", out _));
    }

    // Descriptions come from XML doc comments on the Application DTOs, and reaching the document at
    // all takes four separate things: GenerateDocumentationFile, a direct Microsoft.AspNetCore.OpenApi
    // PackageReference (its build targets do not flow transitively), InterceptorsNamespaces, and an
    // AddOpenApi call site inside this .Api project. Break any one and every description silently
    // disappears while everything still compiles and serves. This is the guard for that.
    [Fact]
    public async Task OpenApiDocument_DescribesSchemaPropertiesFromXmlComments()
    {
        await using var factory = new UserManagementApiFactory();
        var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStreamAsync("/openapi/v1.json"));
        var properties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("CreateOperatorCommand")
            .GetProperty("properties");

        Assert.False(string.IsNullOrWhiteSpace(
            properties.GetProperty("email").GetProperty("description").GetString()));
    }
}

internal sealed class UserManagementApiFactory : WebApplicationFactory<Program>
{
    // Program.cs reads builder.Configuration eagerly in AddUmbralApiDefaults, which runs before the
    // deferred ConfigureAppConfiguration callbacks below. Environment variables are the only channel
    // that reaches it in time: CreateBuilder loads them up front. Removing this throws
    // "Missing required configuration value 'Auth:Authority'" at startup.
    public UserManagementApiFactory()
    {
        Environment.SetEnvironmentVariable("Auth__Authority", "http://localhost:8080/realms/umbral");
        Environment.SetEnvironmentVariable("Auth__Audience", "umbral-user-management-api");
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "http://localhost:8080/realms/umbral",
                ["Auth:Audience"] = "umbral-user-management-api",
                ["Keycloak:BaseUrl"] = "http://localhost:8080",
                ["Keycloak:Realm"] = "umbral",
                ["Keycloak:AdminRealm"] = "master",
                ["Keycloak:AdminClientId"] = "admin-cli",
                ["Keycloak:AdminUsername"] = "admin",
                ["Keycloak:AdminPassword"] = "password",
                ["Logging:EventLog:LogLevel:Default"] = "None"
            });
        });
    }
}
