using System.Net;
using System.Net.Http.Json;
using SessionManagement.Application.Abstractions.Scoring;
using SessionManagement.IntegrationTests.Infrastructure.Fixtures;
using SessionManagement.Infrastructure;
using Xunit;

namespace SessionManagement.IntegrationTests.Infrastructure;

public sealed class ScoringMonitoringHttpClientApplyPenaltyTests
{
    private static ApplyPenaltyRequest BuildRequest() => new(
        LiveSessionId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        SessionTeamId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
        CommandId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
        Severity: "Major",
        AppliedByOperatorUserId: "operator-1",
        Reason: "Out of bounds",
        RecordedAt: DateTimeOffset.UnixEpoch);

    private static ScoringMonitoringHttpClient CreateClient(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://scoring-monitoring.test") });

    [Fact]
    public async Task ApplyPenaltyAsync_DeserializesResponseBody()
    {
        var liveSessionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var sessionTeamId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var commandId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var penaltyId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var scoreEntryId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var expected = new ApplyPenaltyResponse(
            liveSessionId,
            sessionTeamId,
            commandId,
            penaltyId,
            scoreEntryId,
            PenaltyApplied: true,
            VisibleScore: 1500,
            Ranking: new RankingPayload(
                liveSessionId,
                DateTimeOffset.UnixEpoch,
                [new RankingItem(1, sessionTeamId, 1500, TimeSpan.FromSeconds(30))]));
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(expected)
        };
        var client = CreateClient(StubHttpMessageHandler.Returns(response));

        var actual = await client.ApplyPenaltyAsync(BuildRequest(), CancellationToken.None);

        Assert.Equal(penaltyId, actual.PenaltyId);
        Assert.Equal(scoreEntryId, actual.ScoreEntryId);
        Assert.True(actual.PenaltyApplied);
        Assert.Equal(1500, actual.VisibleScore);
        Assert.Equal(commandId, actual.CommandId);
        Assert.Equal(liveSessionId, actual.Ranking.LiveSessionId);
        var rankingItem = Assert.Single(actual.Ranking.Items);
        Assert.Equal(1, rankingItem.Rank);
        Assert.Equal(sessionTeamId, rankingItem.SessionTeamId);
    }

    [Fact]
    public async Task ApplyPenaltyAsync_NonSuccessStatus_ThrowsHttpRequestExceptionWithStatusCode()
    {
        var response = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent("penalty rejected")
        };
        var client = CreateClient(StubHttpMessageHandler.Returns(response));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.ApplyPenaltyAsync(BuildRequest(), CancellationToken.None));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, exception.StatusCode);
        Assert.Contains("422", exception.Message, StringComparison.Ordinal);
        Assert.Contains("penalty rejected", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyPenaltyAsync_SuccessWithEmptyBody_ThrowsJsonException()
    {
        // 204 with no JSON body: ReadFromJsonAsync throws on the empty stream
        // before the null-guard can run, so the JsonException surfaces.
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        var client = CreateClient(StubHttpMessageHandler.Returns(response));

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => client.ApplyPenaltyAsync(BuildRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task ApplyPenaltyAsync_TransportFailure_PropagatesHttpRequestException()
    {
        var transportFailure = new HttpRequestException("connection reset");
        var client = CreateClient(StubHttpMessageHandler.Throws(transportFailure));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.ApplyPenaltyAsync(BuildRequest(), CancellationToken.None));

        Assert.Same(transportFailure, exception);
    }
}
