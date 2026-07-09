using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MissionManagement.Application.Common.Dtos;
using MissionManagement.Domain.Missions;
using MissionManagement.UnitTests;
using Xunit;

namespace MissionManagement.IntegrationTests;

public sealed class MissionEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task CreateMission_ReturnsCreatedMission_WithoutItems()
    {
        await using var factory = new MissionApiFactory();
        var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsJsonAsync(
            "/api/mission-management/missions",
            new CreateMissionRequest("City Circuit", "Route through control points.", 75));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var mission = await response.Content.ReadFromJsonAsync<MissionResponse>(JsonOptions);

        Assert.NotNull(mission);
        Assert.Equal("City Circuit", mission!.Name);
        Assert.False(mission.IsActive);
        Assert.Empty(mission.Items);
    }

    [Fact]
    public async Task CreateMission_PersistsSectionTreeWithChallengesAndPlays()
    {
        await using var factory = new MissionApiFactory();
        var client = factory.CreateAuthorizedClient();

        var request = new CreateMissionRequest(
            "City Circuit",
            "Route through control points.",
            75,
            new[]
            {
                new MissionItemRequest(
                    Kind: MissionItemKind.Section,
                    Order: 1,
                    Title: "Downtown",
                    Children: new[]
                    {
                        new MissionItemRequest(
                            Kind: MissionItemKind.Challenge,
                            Order: 1,
                            Title: "Fountain Hunt",
                            GameType: MissionGameType.TreasureHunt,
                            Difficulty: Difficulty.Medium,
                            TimeLimitMinutes: 15,
                            Searches: new[]
                            {
                                new SearchRequest(null, 1, "Find the fountain", "qr-1", null, null,
                                    new[] { new HintRequest(null, 1, "Near the plaza", false, null, null) })
                            })
                    })
            });

        var response = await client.PostAsJsonAsync("/api/mission-management/missions", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var mission = await response.Content.ReadFromJsonAsync<MissionResponse>(JsonOptions);

        Assert.NotNull(mission);
        var section = Assert.Single(mission!.Items);
        Assert.Equal(MissionItemKind.Section, section.Kind);
        Assert.Equal("Downtown", section.Title);
        var challenge = Assert.Single(section.Children!);
        Assert.Equal(MissionItemKind.Challenge, challenge.Kind);
        Assert.Equal(MissionGameType.TreasureHunt, challenge.GameType);
        var search = Assert.Single(challenge.Searches!);
        Assert.Equal("Find the fountain", search.Clue);
        Assert.Equal("qr-1", search.ExpectedQrHash);
        Assert.Equal("Near the plaza", Assert.Single(search.Hints).Content);
    }

    [Fact]
    public async Task GetMissionById_ReturnsPersistedTree_IncludingCorrectFlagForAdmin()
    {
        await using var factory = new MissionApiFactory();
        var mission = SampleMissions.SingleTrivia(Guid.NewGuid(), "Trivia Mission");
        await factory.SeedMissionAsync(mission);
        var client = factory.CreateAuthorizedClient();

        var detail = await client.GetFromJsonAsync<MissionResponse>(
            $"/api/mission-management/missions/{mission.Id}", JsonOptions);

        Assert.NotNull(detail);
        var challenge = Assert.Single(detail!.Items);
        var question = Assert.Single(challenge.Questions!);
        Assert.Equal("Capital of Venezuela?", question.Text);
        // Admin read side MAY include IsCorrect.
        Assert.Contains(question.Choices, choice => choice.IsCorrect);
    }

    [Fact]
    public async Task ListMissions_ReturnsOrderedSummaries()
    {
        await using var factory = new MissionApiFactory();
        await factory.SeedMissionAsync(Mission.Create(Guid.NewGuid(), "Zulu Mission", "Last.", 120));
        await factory.SeedMissionAsync(Mission.Create(Guid.NewGuid(), "Alpha Mission", "First.", 30));
        var client = factory.CreateAuthorizedClient();

        var missions = await client.GetFromJsonAsync<List<MissionSummaryResponse>>(
            "/api/mission-management/missions", JsonOptions);

        Assert.NotNull(missions);
        Assert.Collection(
            missions!,
            first => Assert.Equal("Alpha Mission", first.Name),
            second => Assert.Equal("Zulu Mission", second.Name));
    }

    [Fact]
    public async Task UpdateMission_ReplacesScalarFieldsAndTree()
    {
        await using var factory = new MissionApiFactory();
        var existing = Mission.Create(Guid.NewGuid(), "Old Mission", "Old.", 45);
        await factory.SeedMissionAsync(existing);
        var client = factory.CreateAuthorizedClient();

        var request = new UpdateMissionRequest(
            "Updated Mission",
            "Updated.",
            95,
            new[]
            {
                new MissionItemRequest(
                    Kind: MissionItemKind.Challenge,
                    Order: 1,
                    Title: "Quiz",
                    GameType: MissionGameType.Trivia,
                    Difficulty: Difficulty.Easy,
                    TimeLimitMinutes: 10,
                    Questions: new[]
                    {
                        new QuestionRequest(null, 1, "2+2?", null, null,
                            new[]
                            {
                                new ChoiceRequest(null, 1, "4", true),
                                new ChoiceRequest(null, 2, "5", false)
                            })
                    })
            });

        var response = await client.PutAsJsonAsync($"/api/mission-management/missions/{existing.Id}", request);
        response.EnsureSuccessStatusCode();

        var mission = await response.Content.ReadFromJsonAsync<MissionResponse>(JsonOptions);

        Assert.NotNull(mission);
        Assert.Equal("Updated Mission", mission!.Name);
        Assert.Equal(95, mission.MaximumDurationMinutes);
        var challenge = Assert.Single(mission.Items);
        Assert.Equal(MissionGameType.Trivia, challenge.GameType);
        Assert.Equal("2+2?", Assert.Single(challenge.Questions!).Text);
    }

    [Fact]
    public async Task ActivateMission_ReturnsActiveMission()
    {
        await using var factory = new MissionApiFactory();
        var mission = SampleMissions.SingleTreasureHunt(Guid.NewGuid(), "Activation Mission");
        await factory.SeedMissionAsync(mission);
        var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsync(
            $"/api/mission-management/missions/{mission.Id}/activate", content: null);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<MissionResponse>(JsonOptions);
        Assert.True(updated!.IsActive);
    }

    [Fact]
    public async Task ActivateMission_ReturnsBadRequest_WhenIneligible()
    {
        await using var factory = new MissionApiFactory();
        var mission = Mission.Create(Guid.NewGuid(), "Draft Mission", "No plays.", 25);
        await factory.SeedMissionAsync(mission);
        var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsync(
            $"/api/mission-management/missions/{mission.Id}/activate", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeactivateMission_ReturnsInactiveMission()
    {
        await using var factory = new MissionApiFactory();
        var mission = SampleMissions.SingleTreasureHunt(Guid.NewGuid(), "Deactivation Mission");
        mission.Activate();
        await factory.SeedMissionAsync(mission);
        var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsync(
            $"/api/mission-management/missions/{mission.Id}/deactivate", content: null);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<MissionResponse>(JsonOptions);
        Assert.False(updated!.IsActive);
    }

    [Fact]
    public async Task ListEligibleMissionsForLiveSession_ReturnsActivePlayCount()
    {
        await using var factory = new MissionApiFactory();
        var eligible = SampleMissions.SingleTreasureHunt(Guid.NewGuid(), "Eligible Mission");
        eligible.Activate();
        await factory.SeedMissionAsync(eligible);
        await factory.SeedMissionAsync(Mission.Create(Guid.NewGuid(), "Draft Mission", "Still drafting.", 30));
        var client = factory.CreateOperatorClient();

        var missions = await client.GetFromJsonAsync<List<EligibleMissionForLiveSessionSummaryResponse>>(
            "/api/mission-management/missions/eligible-for-live-session", JsonOptions);

        Assert.NotNull(missions);
        var mission = Assert.Single(missions!);
        Assert.Equal("Eligible Mission", mission.Name);
        Assert.Equal(1, mission.ActivePlayCount);
    }

    [Fact]
    public async Task GetEligibleMissionForLiveSession_ReturnsFlattenedPlays_TriviaHidesCorrectFlag()
    {
        await using var factory = new MissionApiFactory();
        var mission = SampleMissions.Mixed(Guid.NewGuid());
        mission.Activate();
        await factory.SeedMissionAsync(mission);
        var client = factory.CreateOperatorClient();

        var eligible = await client.GetFromJsonAsync<EligibleMissionForLiveSessionResponse>(
            $"/api/mission-management/missions/eligible-for-live-session/{mission.Id}", JsonOptions);

        Assert.NotNull(eligible);
        Assert.Equal(3, eligible!.Plays.Count);

        var treasure = eligible.Plays[0];
        Assert.Equal(1, treasure.Order);
        Assert.Equal(MissionGameType.TreasureHunt, treasure.GameType);
        Assert.Equal("qr-1", treasure.ExpectedQrHash);
        Assert.Null(treasure.Choices);
        Assert.Null(treasure.CorrectChoiceId);
        Assert.Single(treasure.Hints);

        var trivia = eligible.Plays[2];
        Assert.Equal(MissionGameType.Trivia, trivia.GameType);
        Assert.Equal(Difficulty.Hard, trivia.Difficulty);
        Assert.NotNull(trivia.Choices);
        Assert.Equal(2, trivia.Choices!.Count);
        Assert.NotNull(trivia.CorrectChoiceId);
        Assert.Contains(trivia.Choices, choice => choice.Id == trivia.CorrectChoiceId);
        Assert.Null(trivia.ExpectedQrHash);
        Assert.Empty(trivia.Hints);
    }

    [Fact]
    public async Task GetEligibleMissionForLiveSession_PlayerChoicePayload_NeverExposesCorrectFlag()
    {
        await using var factory = new MissionApiFactory();
        var mission = SampleMissions.Mixed(Guid.NewGuid());
        mission.Activate();
        await factory.SeedMissionAsync(mission);
        var client = factory.CreateOperatorClient();

        // Inspect the raw JSON: a player-facing choice object must not carry any "correct" key.
        var json = await client.GetStringAsync(
            $"/api/mission-management/missions/eligible-for-live-session/{mission.Id}");
        using var document = JsonDocument.Parse(json);

        var plays = document.RootElement.GetProperty("plays");
        foreach (var play in plays.EnumerateArray())
        {
            if (play.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
            {
                foreach (var choice in choices.EnumerateArray())
                {
                    Assert.False(choice.TryGetProperty("isCorrect", out _));
                    Assert.False(choice.TryGetProperty("correct", out _));
                }
            }
        }

        // correctChoiceId is the server-to-server field at the play level.
        Assert.Contains("correctChoiceId", json);
    }

    [Fact]
    public async Task EligibleMissionRoutes_ReturnForbidden_ForParticipant()
    {
        await using var factory = new MissionApiFactory();
        var client = factory.CreateParticipantClient();

        var response = await client.GetAsync("/api/mission-management/missions/eligible-for-live-session");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
