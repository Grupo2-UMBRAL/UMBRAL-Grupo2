using System.Net;
using System.Text.Json;
using SessionManagement.Application.Scoring;
using SessionManagement.Infrastructure;
using SessionManagement.Infrastructure.Persistence;
using Xunit;

namespace SessionManagement.IntegrationTests;

public sealed class ScoringMonitoringHttpClientTests
{
    [Fact]
    public async Task RecordStageCreditAsync_SerializesPlayIdField()
    {
        var liveSessionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var sessionTeamId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var playId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Created));
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://scoring-monitoring.test")
        };
        var scoringClient = new ScoringMonitoringHttpClient(httpClient);

        await scoringClient.RecordStageCreditAsync(
            new RecordStageCreditRequest(
                liveSessionId,
                sessionTeamId,
                playId,
                "Medium",
                TimeSpan.FromSeconds(42),
                DateTimeOffset.UnixEpoch,
                ValidationOverride: false),
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.RequestMethod);
        Assert.Equal($"/api/scoring-monitoring/sessions/{liveSessionId}/scores", handler.RequestPath);
        using var document = JsonDocument.Parse(handler.RequestContent);
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("playId", out var playIdElement));
        Assert.Equal(playId, playIdElement.GetGuid());
        Assert.False(root.TryGetProperty("missionStageId", out _));
        Assert.False(root.TryGetProperty("MissionStageId", out _));
    }

    [Fact]
    public async Task LogSessionEventAsync_PostsMinimumSafePayloadToSessionEventLogEndpoint()
    {
        var liveSessionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Created));
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://scoring-monitoring.test")
        };
        var scoringAuditClient = new ScoringMonitoringHttpClient(httpClient);

        await scoringAuditClient.LogSessionEventAsync(
            liveSessionId,
            "EvidenceSubmitted",
            "Session Team '33333333-3333-3333-3333-333333333333' submitted evidence for Mission Stage '11111111-1111-1111-1111-111111111111'. Game Type: Trivia.",
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.RequestMethod);
        Assert.Equal($"/api/scoring-monitoring/sessions/{liveSessionId}/event-log", handler.RequestPath);
        using var document = JsonDocument.Parse(handler.RequestContent);
        var root = document.RootElement;
        Assert.Equal(2, root.EnumerateObject().Count());
        Assert.Equal("EvidenceSubmitted", GetStringProperty(root, "EventType"));
        Assert.StartsWith("Session Team", GetStringProperty(root, "Description"), StringComparison.Ordinal);
        Assert.False(root.TryGetProperty("liveSessionId", out _));
        Assert.False(root.TryGetProperty("LiveSessionId", out _));
        Assert.False(root.TryGetProperty("submittedHash", out _));
        Assert.False(root.TryGetProperty("SubmittedHash", out _));
        Assert.False(root.TryGetProperty("submittedText", out _));
        Assert.False(root.TryGetProperty("SubmittedText", out _));
    }

    private static string GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var pascalCaseValue))
        {
            return pascalCaseValue.GetString()
                ?? throw new InvalidOperationException($"Property '{propertyName}' was null.");
        }

        var camelCasePropertyName = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
        if (element.TryGetProperty(camelCasePropertyName, out var camelCaseValue))
        {
            return camelCaseValue.GetString()
                ?? throw new InvalidOperationException($"Property '{camelCasePropertyName}' was null.");
        }

        throw new InvalidOperationException($"Property '{propertyName}' was not present.");
    }

    private sealed class CapturingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpMethod? RequestMethod { get; private set; }

        public string? RequestPath { get; private set; }

        public string RequestContent { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestMethod = request.Method;
            RequestPath = request.RequestUri?.AbsolutePath;
            RequestContent = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return response;
        }
    }
}
