using System.Net;
using System.Net.Http.Json;
using MissionManagement.Application.Features.MissionStages;
using MissionManagement.Domain.Missions;
using Umbral.ServiceDefaults;
using Xunit;

namespace MissionManagement.Api.Tests;

public sealed class MissionStageDomainTests
{
    [Fact]
    public void CreateTreasureHuntStage_RequiresExpectedQrHash()
    {
        var exception = Assert.Throws<UmbralDomainException>(() =>
            MissionStage.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Museum Path",
                1,
                MissionStageDifficulty.Easy,
                MissionGameType.TreasureHunt,
                expectedQrHash: null,
                triviaValidationCriteria: null));

        Assert.Equal("mission_stage_expected_qr_hash_required", exception.Code);
        Assert.Equal(UmbralFailureCategory.Validation, exception.Category);
    }

    [Fact]
    public void CreateTriviaStage_RequiresValidationCriteria()
    {
        var exception = Assert.Throws<UmbralDomainException>(() =>
            MissionStage.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Museum Path",
                1,
                MissionStageDifficulty.Medium,
                MissionGameType.Trivia,
                expectedQrHash: null,
                triviaValidationCriteria: null));

        Assert.Equal("mission_stage_trivia_validation_criteria_required", exception.Code);
        Assert.Equal(UmbralFailureCategory.Validation, exception.Category);
    }

    [Fact]
    public void AddHint_RejectsIncompleteCoordinates()
    {
        var stage = MissionStage.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Museum Path",
            1,
            MissionStageDifficulty.Hard,
            MissionGameType.Trivia,
            expectedQrHash: null,
            triviaValidationCriteria: "Answer must match");

        var exception = Assert.Throws<UmbralDomainException>(() =>
            stage.AddHint(
                Guid.NewGuid(),
                "Look near the north entrance.",
                isSolution: false,
                latitude: 10.0,
                longitude: null));

        Assert.Equal("mission_stage_hint_coordinates_incomplete", exception.Code);
        Assert.Equal(UmbralFailureCategory.Validation, exception.Category);
    }
}

public sealed class MissionStageEndpointTests
{
    [Fact]
    public async Task CreateMissionStage_ReturnsCreatedStage_AndListOrdersByStageOrder()
    {
        await using var factory = new MissionApiFactory();
        var mission = Mission.Create(
            Guid.NewGuid(),
            "Stage Mission",
            "Mission with stages.",
            "Medium",
            60,
            MissionGameType.TreasureHunt);
        await factory.SeedMissionAsync(mission);
        var client = factory.CreateAuthorizedClient();

        var secondResponse = await client.PostAsJsonAsync(
            $"/api/mission-management/missions/{mission.Id}/stages",
            new CreateMissionStageRequest(
                "Second Stage",
                2,
                MissionStageDifficulty.Hard,
                MissionGameType.TreasureHunt,
                "qr-hash-2",
                null));
        secondResponse.EnsureSuccessStatusCode();

        var firstResponse = await client.PostAsJsonAsync(
            $"/api/mission-management/missions/{mission.Id}/stages",
            new CreateMissionStageRequest(
                "First Stage",
                1,
                MissionStageDifficulty.Easy,
                MissionGameType.TreasureHunt,
                "qr-hash-1",
                null));
        firstResponse.EnsureSuccessStatusCode();

        var stages = await client.GetFromJsonAsync<List<MissionStageSummaryResponse>>(
            $"/api/mission-management/missions/{mission.Id}/stages");

        Assert.NotNull(stages);
        Assert.Collection(
            stages,
            first =>
            {
                Assert.Equal("First Stage", first.Name);
                Assert.Equal(MissionStageDifficulty.Easy, first.Difficulty);
            },
            second =>
            {
                Assert.Equal("Second Stage", second.Name);
                Assert.Equal(MissionStageDifficulty.Hard, second.Difficulty);
            });
    }

    [Fact]
    public async Task CreateMissionStage_ReturnsConflict_WhenOrderAlreadyExists()
    {
        await using var factory = new MissionApiFactory();
        var mission = Mission.Create(
            Guid.NewGuid(),
            "Conflict Mission",
            "Mission with stage order conflict.",
            "Hard",
            75,
            MissionGameType.Trivia);
        await factory.SeedMissionAsync(mission);
        var client = factory.CreateAuthorizedClient();

        var existingStage = MissionStage.Create(
            Guid.NewGuid(),
            mission.Id,
            "Existing Stage",
            1,
            MissionStageDifficulty.Medium,
            MissionGameType.Trivia,
            expectedQrHash: null,
            triviaValidationCriteria: "Criteria");
        await factory.SeedMissionStageAsync(existingStage);

        var response = await client.PostAsJsonAsync(
            $"/api/mission-management/missions/{mission.Id}/stages",
            new CreateMissionStageRequest(
                "Duplicate Stage",
                1,
                MissionStageDifficulty.Hard,
                MissionGameType.Trivia,
                null,
                "Another criteria"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateMissionStageHint_ReturnsHint_AndStageDetailIncludesIt()
    {
        await using var factory = new MissionApiFactory();
        var mission = Mission.Create(
            Guid.NewGuid(),
            "Hint Mission",
            "Mission with hints.",
            "Easy",
            40,
            MissionGameType.Trivia);
        await factory.SeedMissionAsync(mission);
        var stage = MissionStage.Create(
            Guid.NewGuid(),
            mission.Id,
            "Hint Stage",
            1,
            MissionStageDifficulty.Easy,
            MissionGameType.Trivia,
            expectedQrHash: null,
            triviaValidationCriteria: "Keep going");
        await factory.SeedMissionStageAsync(stage);
        var client = factory.CreateAuthorizedClient();

        var hintResponse = await client.PostAsJsonAsync(
            $"/api/mission-management/stages/{stage.Id}/hints",
            new CreateMissionStageHintRequest(
                "Look under the bench.",
                false,
                10.123,
                -66.321));

        hintResponse.EnsureSuccessStatusCode();

        var stageDetail = await client.GetFromJsonAsync<MissionStageResponse>(
            $"/api/mission-management/stages/{stage.Id}");

        Assert.NotNull(stageDetail);
        Assert.Equal(MissionStageDifficulty.Easy, stageDetail!.Difficulty);
        Assert.Single(stageDetail!.Hints);
        Assert.Equal("Look under the bench.", stageDetail.Hints[0].Content);
    }

    [Fact]
    public async Task DeactivateMissionStage_ReturnsInactiveStage()
    {
        await using var factory = new MissionApiFactory();
        var mission = Mission.Create(
            Guid.NewGuid(),
            "Deactivate Mission",
            "Mission with stage deactivation.",
            "Medium",
            55,
            MissionGameType.TreasureHunt);
        await factory.SeedMissionAsync(mission);
        var stage = MissionStage.Create(
            Guid.NewGuid(),
            mission.Id,
            "Deactivatable Stage",
            1,
            MissionStageDifficulty.Medium,
            MissionGameType.TreasureHunt,
            "qr-hash-1",
            triviaValidationCriteria: null);
        await factory.SeedMissionStageAsync(stage);
        var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsync(
            $"/api/mission-management/stages/{stage.Id}/deactivate",
            content: null);

        response.EnsureSuccessStatusCode();

        var updatedStage = await response.Content.ReadFromJsonAsync<MissionStageResponse>();

        Assert.NotNull(updatedStage);
        Assert.False(updatedStage!.IsActive);
    }
}
