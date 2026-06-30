using System.Net;
using System.Net.Http.Json;
using SessionManagement.Application.Features.LiveSessions;
using SessionManagement.IntegrationTests.Infrastructure.Fixtures;
using SessionManagement.Infrastructure;
using Umbral.ServiceDefaults;
using Xunit;

namespace SessionManagement.IntegrationTests.Infrastructure;

public sealed class MissionManagementLiveSessionCatalogTests
{
    private static readonly Guid MissionId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private static MissionManagementLiveSessionCatalog CreateCatalog(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://mission-management.test") });

    [Fact]
    public async Task GetEligibleMissionForLiveSessionAsync_HappyPath_DeserializesSnapshot()
    {
        var expected = new EligibleMissionForLiveSessionSnapshot(
            MissionId,
            "Operation Nightfall",
            "A timed mission.",
            MaximumDurationMinutes: 90,
            Plays:
            [
                new EligiblePlaySnapshot(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Order: 1,
                    GameType: "Trivia",
                    Difficulty: "Medium",
                    TimeLimitMinutes: 10,
                    Prompt: "What year?",
                    Choices: [new EligibleChoiceSnapshot(Guid.Parse("22222222-2222-2222-2222-222222222222"), "1989")],
                    CorrectChoiceId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    ExpectedQrHash: null,
                    Hints: null)
            ]);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(expected)
        };
        var handler = StubHttpMessageHandler.Returns(response);
        var catalog = CreateCatalog(handler);

        var actual = await catalog.GetEligibleMissionForLiveSessionAsync(MissionId, CancellationToken.None);

        Assert.Equal(HttpMethod.Get, handler.RequestMethod);
        Assert.Equal($"/api/mission-management/missions/eligible-for-live-session/{MissionId}", handler.RequestPath);
        Assert.Equal(MissionId, actual.Id);
        Assert.Equal("Operation Nightfall", actual.Name);
        Assert.Equal(90, actual.MaximumDurationMinutes);
        var play = Assert.Single(actual.Plays);
        Assert.Equal("Trivia", play.GameType);
        var choice = Assert.Single(play.Choices!);
        Assert.Equal("1989", choice.Text);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, UmbralFailureCategory.Validation)]
    [InlineData(HttpStatusCode.Unauthorized, UmbralFailureCategory.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound, UmbralFailureCategory.NotFound)]
    public async Task GetEligibleMissionForLiveSessionAsync_ClientError_MapsToDomainExceptionByStatus(
        HttpStatusCode statusCode,
        UmbralFailureCategory expectedCategory)
    {
        // Empty body forces fallback to status-based code + category mapping.
        var response = new HttpResponseMessage(statusCode);
        var catalog = CreateCatalog(StubHttpMessageHandler.Returns(response));

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(
            () => catalog.GetEligibleMissionForLiveSessionAsync(MissionId, CancellationToken.None));

        Assert.Equal(expectedCategory, exception.Category);
        Assert.Equal($"mission_management_live_session_catalog_{(int)statusCode}", exception.Code);
    }

    [Fact]
    public async Task GetEligibleMissionForLiveSessionAsync_ProblemDetailsBody_UsesUpstreamCodeDetailAndCategory()
    {
        // The upstream problem-details record is deserialized with the default
        // case-sensitive options, so the keys must match the PascalCase property names.
        var problemBody = """
        {
            "Detail": "Mission is archived and cannot host a live session.",
            "Code": "mission_not_eligible",
            "Category": "Conflict"
        }
        """;
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(problemBody, System.Text.Encoding.UTF8, "application/json")
        };
        var catalog = CreateCatalog(StubHttpMessageHandler.Returns(response));

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(
            () => catalog.GetEligibleMissionForLiveSessionAsync(MissionId, CancellationToken.None));

        Assert.Equal("mission_not_eligible", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
        Assert.Equal("Mission is archived and cannot host a live session.", exception.Message);
    }

    [Fact]
    public async Task GetEligibleMissionForLiveSessionAsync_ServerError_MapsToTechnicalException()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var catalog = CreateCatalog(StubHttpMessageHandler.Returns(response));

        var exception = await Assert.ThrowsAsync<UmbralTechnicalException>(
            () => catalog.GetEligibleMissionForLiveSessionAsync(MissionId, CancellationToken.None));

        Assert.Equal("mission_management_live_session_catalog_500", exception.Code);
        Assert.Equal(UmbralFailureCategory.Technical, exception.Category);
    }

    [Fact]
    public async Task GetEligibleMissionForLiveSessionAsync_SuccessWithMalformedJsonBody_ThrowsJsonException()
    {
        // 200 with a non-deserializable body: success path uses ReadFromJsonAsync which throws.
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ this is not valid json", System.Text.Encoding.UTF8, "application/json")
        };
        var catalog = CreateCatalog(StubHttpMessageHandler.Returns(response));

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => catalog.GetEligibleMissionForLiveSessionAsync(MissionId, CancellationToken.None));
    }
}
