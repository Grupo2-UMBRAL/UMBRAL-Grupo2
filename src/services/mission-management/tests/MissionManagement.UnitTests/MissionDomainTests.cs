using MissionManagement.Domain.Missions;
using Umbral.ServiceDefaults;
using Xunit;

namespace MissionManagement.UnitTests;

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
