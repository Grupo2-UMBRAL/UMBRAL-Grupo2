using System.Net;
using System.Text.Json;
using Xunit;

namespace ScoringMonitoring.IntegrationTests;

public sealed class OpenApiContractTests
{
    private const string RankingPath = "/api/scoring-monitoring/sessions/{liveSessionId}/ranking";

    [Fact]
    public async Task Api_PublishesOpenApiDocumentAndInteractiveReference()
    {
        await using var factory = new ScoringApiFactory();
        var client = factory.CreateClient();

        var documentResponse = await client.GetAsync("/openapi/v1.json");
        var referenceResponse = await client.GetAsync("/swagger");

        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        Assert.Equal("application/json", documentResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, referenceResponse.StatusCode);
        Assert.Equal("text/html", referenceResponse.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await documentResponse.Content.ReadAsStreamAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty(RankingPath, out _));
        Assert.False(paths.TryGetProperty("/health", out _));
        Assert.True(document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .TryGetProperty("Bearer", out _));
    }

    // The SignalR hub speaks its own protocol and has no place in an HTTP contract. It is absent
    // because ApiExplorer never sees MapHub, so this pins a property nothing else enforces.
    [Fact]
    public async Task OpenApiDocument_ExcludesRealtimeHub()
    {
        await using var factory = new ScoringApiFactory();
        var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStreamAsync("/openapi/v1.json"));

        Assert.False(document.RootElement.GetProperty("paths").TryGetProperty("/hub/scoring", out _));
    }

    [Fact]
    public async Task OpenApiDocument_DescribesRankingOperation()
    {
        await using var factory = new ScoringApiFactory();
        var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStreamAsync("/openapi/v1.json"));
        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty(RankingPath)
            .GetProperty("get");

        Assert.Equal("Get ranking", operation.GetProperty("summary").GetString());
        Assert.True(operation.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(operation.GetProperty("responses").TryGetProperty("401", out _));
        Assert.True(operation.GetProperty("responses").TryGetProperty("403", out _));
    }

    // Served straight from the service, the framework's own server URL is already correct.
    [Fact]
    public async Task OpenApiDocument_AdvertisesRequestedHostWhenNotProxied()
    {
        await using var factory = new ScoringApiFactory();
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
        await using var factory = new ScoringApiFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/openapi/v1.json");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", "umbral.example.org");
        request.Headers.Add("X-Forwarded-Prefix", "/scoring-monitoring");

        var response = await client.SendAsync(request);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var server = document.RootElement.GetProperty("servers")[0].GetProperty("url").GetString();

        Assert.Equal("https://umbral.example.org/scoring-monitoring", server);

        // The prefix rides on the server URL, never on the paths: OpenAPI resolves a call as
        // server + path, so duplicating it in both would double it.
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty(RankingPath, out _));
    }

    // Descriptions come from XML doc comments on the Application DTOs, and reaching the document at
    // all takes four separate things: GenerateDocumentationFile, a direct Microsoft.AspNetCore.OpenApi
    // PackageReference (its build targets do not flow transitively), InterceptorsNamespaces, and an
    // AddOpenApi call site inside this .Api project. Break any one and every description silently
    // disappears while everything still compiles and serves. This is the guard for that.
    [Fact]
    public async Task OpenApiDocument_DescribesSchemaPropertiesFromXmlComments()
    {
        await using var factory = new ScoringApiFactory();
        var client = factory.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStreamAsync("/openapi/v1.json"));
        var properties = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("ApplyPenaltyRequest")
            .GetProperty("properties");

        Assert.False(string.IsNullOrWhiteSpace(
            properties.GetProperty("sessionTeamId").GetProperty("description").GetString()));
    }
}
