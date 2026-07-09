using MissionManagement.Application.Common.Dtos;
using MissionManagement.Application.Common.Mappings;
using MissionManagement.Domain.Missions;
using Umbral.ServiceDefaults;
using Xunit;

namespace MissionManagement.UnitTests;

public sealed class MissionMappingsTests
{
    // -----------------------------------------------------------------------
    // domain -> response
    // -----------------------------------------------------------------------

    [Fact]
    public void ToResponse_MapsScalarFields_AndRootItems()
    {
        var mission = SampleMissions.Mixed(Guid.NewGuid());

        var response = mission.ToResponse();

        Assert.Equal(mission.Id, response.Id);
        Assert.Equal(mission.Name, response.Name);
        Assert.Equal(mission.Description, response.Description);
        Assert.Equal(mission.MaximumDurationMinutes, response.MaximumDurationMinutes);
        Assert.False(response.IsActive);
        Assert.Equal(mission.RootItems.Count, response.Items.Count);
    }

    [Fact]
    public void ToResponse_MapsSectionWithChildren_AndNullsChallengeFields()
    {
        var mission = SampleMissions.Mixed(Guid.NewGuid());

        var response = mission.ToResponse();

        var section = Assert.Single(response.Items, item => item.Kind == MissionItemKind.Section);
        Assert.NotNull(section.Children);
        Assert.NotEmpty(section.Children!);
        // Section carries no challenge semantics.
        Assert.Null(section.GameType);
        Assert.Null(section.Difficulty);
        Assert.Null(section.TimeLimitMinutes);
        Assert.Null(section.IsActive);
        Assert.Null(section.Questions);
        Assert.Null(section.Searches);
    }

    [Fact]
    public void ToResponse_MapsChallengeWithQuestionsAndChoices()
    {
        var mission = SampleMissions.SingleTrivia(Guid.NewGuid(), "Quiz Mission");

        var response = mission.ToResponse();

        var challenge = Assert.Single(response.Items, item => item.Kind == MissionItemKind.Challenge);
        Assert.Null(challenge.Children);
        Assert.Equal(MissionGameType.Trivia, challenge.GameType);
        Assert.Equal(Difficulty.Medium, challenge.Difficulty);
        Assert.NotNull(challenge.TimeLimitMinutes);
        Assert.True(challenge.IsActive);
        Assert.NotNull(challenge.Questions);
        var question = Assert.Single(challenge.Questions!);
        Assert.Equal(2, question.Choices.Count);
        Assert.Contains(question.Choices, choice => choice.IsCorrect);
        // No searches on a trivia challenge.
        Assert.NotNull(challenge.Searches);
        Assert.Empty(challenge.Searches!);
    }

    [Fact]
    public void ToResponse_MapsChallengeWithSearchesAndHints()
    {
        var mission = SampleMissions.Mixed(Guid.NewGuid());

        var response = mission.ToResponse();

        var section = Assert.Single(response.Items, item => item.Kind == MissionItemKind.Section);
        var hunt = Assert.Single(section.Children!, child => child.Kind == MissionItemKind.Challenge);
        Assert.NotNull(hunt.Searches);
        Assert.Equal(2, hunt.Searches!.Count);
        var firstSearch = hunt.Searches!.Single(search => search.Order == 1);
        Assert.Equal("qr-1", firstSearch.ExpectedQrHash);
        Assert.Single(firstSearch.Hints);
        var hint = firstSearch.Hints[0];
        Assert.Equal(10.5, hint.Latitude);
        Assert.Equal(-66.9, hint.Longitude);
        // Search without hints maps to an empty list, not null.
        var secondSearch = hunt.Searches!.Single(search => search.Order == 2);
        Assert.Empty(secondSearch.Hints);
        // No questions on a treasure-hunt challenge.
        Assert.NotNull(hunt.Questions);
        Assert.Empty(hunt.Questions!);
    }

    [Fact]
    public void ToResponse_RejectsNullMission()
    {
        Mission mission = null!;

        Assert.Throws<ArgumentNullException>(() => mission.ToResponse());
    }

    [Fact]
    public void ToSummaryResponse_MapsScalarFields()
    {
        var mission = SampleMissions.SingleTrivia(Guid.NewGuid(), "Quiz Mission");

        var summary = mission.ToSummaryResponse();

        Assert.Equal(mission.Id, summary.Id);
        Assert.Equal(mission.Name, summary.Name);
        Assert.Equal(mission.MaximumDurationMinutes, summary.MaximumDurationMinutes);
        Assert.False(summary.IsActive);
    }

    [Fact]
    public void ToSummaryResponse_RejectsNullMission()
    {
        Mission mission = null!;

        Assert.Throws<ArgumentNullException>(() => mission.ToSummaryResponse());
    }

    // -----------------------------------------------------------------------
    // eligible-for-live-session
    // -----------------------------------------------------------------------

    [Fact]
    public void ToEligibleForLiveSessionSummaryResponse_CountsFlattenedPlays()
    {
        var mission = SampleMissions.Mixed(Guid.NewGuid());

        var summary = mission.ToEligibleForLiveSessionSummaryResponse();

        Assert.Equal(mission.Id, summary.Id);
        Assert.Null(summary.GameType);
        Assert.Equal(3, summary.ActivePlayCount);
    }

    [Fact]
    public void ToEligibleForLiveSessionSummaryResponse_RejectsNullMission()
    {
        Mission mission = null!;

        Assert.Throws<ArgumentNullException>(() => mission.ToEligibleForLiveSessionSummaryResponse());
    }

    [Fact]
    public void ToEligibleForLiveSessionResponse_ProjectsChoices_WhenQuestionPlay()
    {
        var mission = SampleMissions.SingleTrivia(Guid.NewGuid(), "Quiz Mission");

        var response = mission.ToEligibleForLiveSessionResponse();

        var play = Assert.Single(response.Plays);
        Assert.Equal(MissionGameType.Trivia, play.GameType);
        Assert.NotNull(play.Choices);
        Assert.Equal(2, play.Choices!.Count);
        Assert.NotNull(play.CorrectChoiceId);
        Assert.Empty(play.Hints);
    }

    [Fact]
    public void ToEligibleForLiveSessionResponse_NullsChoices_WhenSearchPlay()
    {
        var mission = SampleMissions.SingleTreasureHunt(Guid.NewGuid(), "Gate Hunt");

        var response = mission.ToEligibleForLiveSessionResponse();

        var play = Assert.Single(response.Plays);
        Assert.Equal(MissionGameType.TreasureHunt, play.GameType);
        // A search play has no choices -> the optional collection is null.
        Assert.Null(play.Choices);
        Assert.Null(play.CorrectChoiceId);
        Assert.Equal("qr-hash-1", play.ExpectedQrHash);
    }

    [Fact]
    public void ToEligibleForLiveSessionResponse_MapsHints_WhenSearchHasThem()
    {
        var mission = SampleMissions.Mixed(Guid.NewGuid());

        var response = mission.ToEligibleForLiveSessionResponse();

        var searchWithHint = response.Plays.Single(play => play.ExpectedQrHash == "qr-1");
        var hint = Assert.Single(searchWithHint.Hints);
        Assert.Equal(10.5, hint.Latitude);
        Assert.Equal(-66.9, hint.Longitude);
        Assert.False(hint.IsSolution);
    }

    [Fact]
    public void ToEligibleForLiveSessionResponse_RejectsNullMission()
    {
        Mission mission = null!;

        Assert.Throws<ArgumentNullException>(() => mission.ToEligibleForLiveSessionResponse());
    }

    // -----------------------------------------------------------------------
    // request -> domain
    // -----------------------------------------------------------------------

    [Fact]
    public void ToDomain_MapsSectionWithNestedChallenge()
    {
        var missionId = Guid.NewGuid();
        var items = new List<MissionItemRequest>
        {
            new(
                Kind: MissionItemKind.Section,
                Order: 1,
                Title: "Downtown",
                Children: new List<MissionItemRequest>
                {
                    new(
                        Kind: MissionItemKind.Challenge,
                        Order: 1,
                        Title: "Hunt",
                        GameType: MissionGameType.TreasureHunt,
                        Difficulty: Difficulty.Easy,
                        TimeLimitMinutes: 15,
                        Searches: new List<SearchRequest>
                        {
                            new(null, 1, "Find it", "qr-1", null, null,
                                new List<HintRequest> { new(null, 1, "Look north", false, null, null) })
                        })
                })
        };

        var rootItems = items.ToDomain(missionId);

        var section = Assert.IsType<Section>(Assert.Single(rootItems));
        Assert.Equal(missionId, section.MissionId);
        Assert.Null(section.ParentSectionId);
        var challenge = Assert.IsType<Challenge>(Assert.Single(section.Children));
        Assert.Equal(section.Id, challenge.ParentSectionId);
        Assert.Single(challenge.Plays);
    }

    [Fact]
    public void ToDomain_MapsChallengeWithQuestion_AndNoOptionalPlays()
    {
        var missionId = Guid.NewGuid();
        var items = new List<MissionItemRequest>
        {
            new(
                Kind: MissionItemKind.Challenge,
                Order: 1,
                Title: "Quiz",
                GameType: MissionGameType.Trivia,
                Difficulty: Difficulty.Medium,
                TimeLimitMinutes: 10,
                Questions: new List<QuestionRequest>
                {
                    new(null, 1, "Capital?", null, null,
                        new List<ChoiceRequest>
                        {
                            new(null, 1, "Caracas", true),
                            new(null, 2, "Valencia", false)
                        })
                })
        };

        var rootItems = items.ToDomain(missionId);

        var challenge = Assert.IsType<Challenge>(Assert.Single(rootItems));
        var question = Assert.IsType<Question>(Assert.Single(challenge.Plays));
        Assert.Equal(2, question.Choices.Count);
    }

    [Fact]
    public void ToDomain_RejectsUnsupportedItemKind()
    {
        var items = new List<MissionItemRequest> { new(Kind: "Wormhole", Order: 1) };

        var exception = Assert.Throws<UmbralDomainException>(() => items.ToDomain(Guid.NewGuid()));

        Assert.Equal("mission_item_kind_unsupported", exception.Code);
        Assert.Equal(UmbralFailureCategory.Validation, exception.Category);
    }

    [Fact]
    public void ToDomain_RejectsNullItems()
    {
        IReadOnlyList<MissionItemRequest> items = null!;

        Assert.Throws<ArgumentNullException>(() => items.ToDomain(Guid.NewGuid()));
    }
}
