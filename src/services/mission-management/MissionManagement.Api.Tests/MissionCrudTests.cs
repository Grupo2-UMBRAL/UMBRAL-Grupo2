using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionManagement.Application.Features.Missions;
using MissionManagement.Domain.Missions;
using MissionManagement.Infrastructure.Persistence;
using Umbral.ServiceDefaults;
using Xunit;

namespace MissionManagement.Api.Tests;

public sealed class MissionDomainTests
{
    [Fact]
    public void Create_NormalizesNameAndDescription_AndMarksMissionInactive()
    {
        var mission = Mission.Create(
            Guid.NewGuid(),
            "  Caracas Chase  ",
            " Urban treasure route ",
            90);

        Assert.Equal("Caracas Chase", mission.Name);
        Assert.Equal("Urban treasure route", mission.Description);
        Assert.False(mission.IsActive);
        Assert.Empty(mission.RootItems);
    }

    [Fact]
    public void Create_RejectsMaximumDurationOutOfRange()
    {
        var exception = Assert.Throws<UmbralDomainException>(() =>
            Mission.Create(Guid.NewGuid(), "Night Run", "Description", 0));

        Assert.Equal("mission_maximum_duration_invalid", exception.Code);
        Assert.Equal(UmbralFailureCategory.Validation, exception.Category);
    }

    [Fact]
    public void Section_IsInert_AndMayBeEmpty()
    {
        var section = Section.Create(Guid.NewGuid(), Guid.NewGuid(), null, 1, "Intro");

        Assert.Equal("Intro", section.Title);
        Assert.Empty(section.Children);
    }

    [Fact]
    public void Challenge_RejectsPlayGameTypeMismatch()
    {
        var challengeId = Guid.NewGuid();
        var search = Search.Create(Guid.NewGuid(), challengeId, 1, "Find the crest", "qr-hash", null, null, null);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            Challenge.Create(
                challengeId,
                Guid.NewGuid(),
                null,
                1,
                "Trivia block",
                MissionGameType.Trivia,
                Difficulty.Easy,
                10,
                true,
                new Play[] { search }));

        Assert.Equal("challenge_play_game_type_mismatch", exception.Code);
    }

    [Fact]
    public void Question_RequiresBetweenTwoAndFourChoices()
    {
        var questionId = Guid.NewGuid();
        var exception = Assert.Throws<UmbralDomainException>(() =>
            Question.Create(
                questionId,
                Guid.NewGuid(),
                1,
                "Only one choice?",
                null,
                null,
                new[] { Choice.Create(Guid.NewGuid(), questionId, 1, "Solo", true) }));

        Assert.Equal("question_choice_count_invalid", exception.Code);
    }

    [Fact]
    public void Question_RequiresExactlyOneCorrectChoice()
    {
        var questionId = Guid.NewGuid();
        var exception = Assert.Throws<UmbralDomainException>(() =>
            Question.Create(
                questionId,
                Guid.NewGuid(),
                1,
                "Which one?",
                null,
                null,
                new[]
                {
                    Choice.Create(Guid.NewGuid(), questionId, 1, "A", true),
                    Choice.Create(Guid.NewGuid(), questionId, 2, "B", true)
                }));

        Assert.Equal("question_correct_choice_required", exception.Code);
    }

    [Fact]
    public void Search_RequiresExpectedQrHash()
    {
        var exception = Assert.Throws<UmbralDomainException>(() =>
            Search.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Clue", "  ", null, null, null));

        Assert.Equal("search_expected_qr_hash_required", exception.Code);
    }

    [Fact]
    public void Hint_RejectsIncompleteCoordinates()
    {
        var exception = Assert.Throws<UmbralDomainException>(() =>
            Hint.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Look north", false, latitude: 10.1, longitude: null));

        Assert.Equal("hint_coordinates_incomplete", exception.Code);
    }

    [Fact]
    public void Flatten_DescendsSections_SkipsInactiveChallenges_AndAssignsGlobalOrder()
    {
        var mission = SampleMissions.Mixed(Guid.NewGuid());

        var plays = mission.Flatten();

        // Active challenges only: the inactive trivia challenge is skipped.
        Assert.Equal(3, plays.Count);
        Assert.Equal(new[] { 1, 2, 3 }, plays.Select(play => play.Order).ToArray());

        // Depth-first, in-order: nested section's treasure-hunt searches come first, then root trivia.
        Assert.Equal(MissionGameType.TreasureHunt, plays[0].GameType);
        Assert.Equal("Find the fountain", plays[0].Prompt);
        Assert.Equal("qr-1", plays[0].ExpectedQrHash);
        Assert.Equal(MissionGameType.TreasureHunt, plays[1].GameType);
        Assert.Equal(MissionGameType.Trivia, plays[2].GameType);
        Assert.Equal("Capital of Venezuela?", plays[2].Prompt);

        // Difficulty override resolution on the trivia play.
        Assert.Equal(Difficulty.Hard, plays[2].Difficulty);
        // Choices carry ids+text; correct choice id exposed at top level only.
        Assert.Equal(2, plays[2].Choices.Count);
        Assert.NotNull(plays[2].CorrectChoiceId);
        Assert.Contains(plays[2].Choices, choice => choice.Id == plays[2].CorrectChoiceId);
    }

    [Fact]
    public void Activate_RejectsMissionWithoutActivePlay()
    {
        var mission = Mission.Create(Guid.NewGuid(), "Empty", "No plays", 30);

        var exception = Assert.Throws<UmbralDomainException>(mission.Activate);

        Assert.Equal("mission_eligible_play_required", exception.Code);
    }

    [Fact]
    public void Activate_AllowsEligibleMission()
    {
        var mission = SampleMissions.SingleTreasureHunt(Guid.NewGuid(), "Museum Hunt");

        mission.Activate();

        Assert.True(mission.IsActive);
    }

    [Fact]
    public void InactiveChallenge_RendersMissionIneligible()
    {
        var missionId = Guid.NewGuid();
        var challengeId = Guid.NewGuid();
        var search = Search.Create(Guid.NewGuid(), challengeId, 1, "Clue", "qr", null, null, null);
        var challenge = Challenge.Create(
            challengeId, missionId, null, 1, "Hunt", MissionGameType.TreasureHunt, Difficulty.Easy, 10, isActive: false,
            new Play[] { search });
        var mission = Mission.Create(missionId, "Dormant", "All inactive", 30, new PathItem[] { challenge });

        Assert.False(mission.IsEligibleForLiveSession());
    }
}

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

/// <summary>Reusable sample aggregates for the new linear-path model.</summary>
internal static class SampleMissions
{
    public static Mission SingleTreasureHunt(Guid missionId, string name)
    {
        var challengeId = Guid.NewGuid();
        var search = Search.Create(Guid.NewGuid(), challengeId, 1, "Scan the opening gate", "qr-hash-1", null, null, null);
        var challenge = Challenge.Create(
            challengeId, missionId, null, 1, "Gate Hunt", MissionGameType.TreasureHunt, Difficulty.Easy, 20, true,
            new Play[] { search });

        return Mission.Create(missionId, name, "Description.", 25, new PathItem[] { challenge });
    }

    public static Mission SingleTrivia(Guid missionId, string name)
    {
        var challengeId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var question = Question.Create(
            questionId, challengeId, 1, "Capital of Venezuela?", null, null,
            new[]
            {
                Choice.Create(Guid.NewGuid(), questionId, 1, "Caracas", true),
                Choice.Create(Guid.NewGuid(), questionId, 2, "Maracaibo", false)
            });
        var challenge = Challenge.Create(
            challengeId, missionId, null, 1, "Quiz", MissionGameType.Trivia, Difficulty.Medium, 10, true,
            new Play[] { question });

        return Mission.Create(missionId, name, "Description.", 30, new PathItem[] { challenge });
    }

    /// <summary>
    /// Root order 1 = a Section containing a treasure-hunt Challenge (2 searches);
    /// root order 2 = an active trivia Challenge (1 question, Hard override);
    /// root order 3 = an INACTIVE trivia Challenge (skipped by the flatten).
    /// </summary>
    public static Mission Mixed(Guid missionId)
    {
        var sectionId = Guid.NewGuid();

        var huntId = Guid.NewGuid();
        var firstSearchId = Guid.NewGuid();
        var hunt = Challenge.Create(
            huntId, missionId, sectionId, 1, "Hunt", MissionGameType.TreasureHunt, Difficulty.Easy, 15, true,
            new Play[]
            {
                Search.Create(firstSearchId, huntId, 1, "Find the fountain", "qr-1", null, null,
                    new[] { Hint.Create(Guid.NewGuid(), firstSearchId, 1, "Near the plaza", false, 10.5, -66.9) }),
                Search.Create(Guid.NewGuid(), huntId, 2, "Find the statue", "qr-2", null, null, null)
            });
        var section = Section.Create(sectionId, missionId, null, 1, "Downtown", new PathItem[] { hunt });

        var triviaId = Guid.NewGuid();
        var triviaQuestionId = Guid.NewGuid();
        var trivia = Challenge.Create(
            triviaId, missionId, null, 2, "Quiz", MissionGameType.Trivia, Difficulty.Easy, 10, true,
            new Play[]
            {
                Question.Create(triviaQuestionId, triviaId, 1, "Capital of Venezuela?", Difficulty.Hard, null,
                    new[]
                    {
                        Choice.Create(Guid.NewGuid(), triviaQuestionId, 1, "Caracas", true),
                        Choice.Create(Guid.NewGuid(), triviaQuestionId, 2, "Valencia", false)
                    })
            });

        var inactiveId = Guid.NewGuid();
        var inactiveQuestionId = Guid.NewGuid();
        var inactive = Challenge.Create(
            inactiveId, missionId, null, 3, "Skipped", MissionGameType.Trivia, Difficulty.Easy, 10, isActive: false,
            new Play[]
            {
                Question.Create(inactiveQuestionId, inactiveId, 1, "Skipped?", null, null,
                    new[]
                    {
                        Choice.Create(Guid.NewGuid(), inactiveQuestionId, 1, "Yes", true),
                        Choice.Create(Guid.NewGuid(), inactiveQuestionId, 2, "No", false)
                    })
            });

        return Mission.Create(missionId, "Tree Mission", "Ready for session.", 60,
            new PathItem[] { section, trivia, inactive });
    }
}

internal sealed class MissionApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"mission-management-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=umbral;Username=umbral;Password=umbral",
                ["Auth:Authority"] = "http://localhost:8080/realms/umbral",
                ["Auth:Audience"] = "umbral-mission-management-api",
                ["Persistence:ApplyMigrationsOnStartup"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });

            services.RemoveAll<DbContextOptions<MissionManagementDbContext>>();
            services.RemoveAll<MissionManagementDbContext>();

            services.AddScoped<MissionManagementDbContext>(sp =>
            {
                var options = new DbContextOptionsBuilder<MissionManagementDbContext>()
                    .UseInMemoryDatabase(databaseName)
                    .Options;
                return new MissionManagementDbContext(options);
            });

            services.AddScoped<DbContextOptions<MissionManagementDbContext>>(sp =>
            {
                return new DbContextOptionsBuilder<MissionManagementDbContext>()
                    .UseInMemoryDatabase(databaseName)
                    .Options;
            });
        });
    }

    public HttpClient CreateAuthorizedClient() => CreateClientForRole(UmbralRoles.Administrator);

    public HttpClient CreateOperatorClient() => CreateClientForRole(UmbralRoles.Operator);

    public HttpClient CreateParticipantClient() => CreateClientForRole(UmbralRoles.Participant);

    public async Task SeedMissionAsync(Mission mission)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MissionManagementDbContext>();

        await dbContext.Database.EnsureCreatedAsync();
        dbContext.Missions.Add(mission);
        foreach (var item in EnumerateDepthFirst(mission.RootItems))
        {
            dbContext.PathItems.Add(item);
        }

        await dbContext.SaveChangesAsync();
    }

    private static IEnumerable<PathItem> EnumerateDepthFirst(IReadOnlyList<PathItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            if (item is Section section)
            {
                foreach (var child in EnumerateDepthFirst(section.Children))
                {
                    yield return child;
                }
            }
        }
    }

    private HttpClient CreateClientForRole(string role)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, role);

        return client;
    }

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string RoleHeaderName = "X-Test-Role";

        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(RoleHeaderName))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = Request.Headers[RoleHeaderName]
                .Where(static role => !string.IsNullOrWhiteSpace(role))
                .Select(role => new Claim(ClaimTypes.Role, role!));
            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
