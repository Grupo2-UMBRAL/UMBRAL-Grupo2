using Umbral.ServiceDefaults;
using SessionManagement.Domain.LiveSessions;
using Xunit;

namespace SessionManagement.UnitTests;

public sealed class LiveSessionStageTests
{
    private static readonly Guid StageId = Guid.Parse("dddddddd-dddd-dddd-dddd-000000000001");

    [Fact]
    public void Create_NormalizesFields_AndDefaultsOptionalCollections()
    {
        var stage = LiveSessionStage.Create(
            StageId,
            "  Stage One  ",
            sessionStageOrder: 1,
            sourceOrder: 1,
            resolvedTimeBudgetMinutes: 10,
            difficulty: "  Medium  ",
            gameType: "  TreasureHunt  ",
            prompt: "  Find it  ",
            expectedQrHash: "  qr  ");

        Assert.Equal("Stage One", stage.Name);
        Assert.Equal("Medium", stage.Difficulty);
        Assert.Equal("TreasureHunt", stage.GameType);
        Assert.Equal("Find it", stage.Prompt);
        Assert.Equal("qr", stage.ExpectedQrHash);
        Assert.Empty(stage.Choices);
        Assert.Empty(stage.Hints);
        Assert.Null(stage.CorrectChoiceId);
    }

    [Fact]
    public void Create_TreatsBlankExpectedQrHash_AsNull()
    {
        var stage = LiveSessionStage.Create(
            StageId, "Stage", 1, 1, 10, "Easy", "Trivia", "Prompt", expectedQrHash: "   ");

        Assert.Null(stage.ExpectedQrHash);
    }

    [Fact]
    public void Create_Throws_WhenMissionStageIdEmpty()
        => AssertCreateThrows(
            "live_session_stage_source_required",
            () => LiveSessionStage.Create(Guid.Empty, "Stage", 1, 1, 10, "Easy", "Trivia", "Prompt"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Throws_WhenNameBlank(string name)
        => AssertCreateThrows(
            "live_session_stage_name_required",
            () => LiveSessionStage.Create(StageId, name, 1, 1, 10, "Easy", "Trivia", "Prompt"));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_Throws_WhenSessionStageOrderNotPositive(int order)
        => AssertCreateThrows(
            "live_session_stage_order_invalid",
            () => LiveSessionStage.Create(StageId, "Stage", order, 1, 10, "Easy", "Trivia", "Prompt"));

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Create_Throws_WhenSourceOrderNotPositive(int sourceOrder)
        => AssertCreateThrows(
            "live_session_stage_source_order_invalid",
            () => LiveSessionStage.Create(StageId, "Stage", 1, sourceOrder, 10, "Easy", "Trivia", "Prompt"));

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_Throws_WhenTimeBudgetNotPositive(int minutes)
        => AssertCreateThrows(
            "live_session_stage_time_budget_invalid",
            () => LiveSessionStage.Create(StageId, "Stage", 1, 1, minutes, "Easy", "Trivia", "Prompt"));

    [Fact]
    public void Create_Throws_WhenDifficultyBlank()
        => AssertCreateThrows(
            "live_session_stage_difficulty_required",
            () => LiveSessionStage.Create(StageId, "Stage", 1, 1, 10, "  ", "Trivia", "Prompt"));

    [Fact]
    public void Create_Throws_WhenGameTypeBlank()
        => AssertCreateThrows(
            "live_session_stage_game_type_required",
            () => LiveSessionStage.Create(StageId, "Stage", 1, 1, 10, "Easy", "  ", "Prompt"));

    [Fact]
    public void Create_Throws_WhenPromptBlank()
        => AssertCreateThrows(
            "live_session_stage_prompt_required",
            () => LiveSessionStage.Create(StageId, "Stage", 1, 1, 10, "Easy", "Trivia", "  "));

    [Fact]
    public void Create_Throws_WhenCorrectChoiceNotAmongChoices()
        => AssertCreateThrows(
            "live_session_stage_correct_choice_not_in_choices",
            () => LiveSessionStage.Create(
                StageId, "Stage", 1, 1, 10, "Easy", "Trivia", "Prompt",
                choices: [LiveSessionChoice.Create(Guid.NewGuid(), "A")],
                correctChoiceId: Guid.NewGuid()));

    private static void AssertCreateThrows(string expectedCode, Func<LiveSessionStage> act)
    {
        var exception = Assert.Throws<UmbralDomainException>(() => act());
        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(UmbralFailureCategory.Validation, exception.Category);
    }
}

public sealed class ReleasedHintTests
{
    private static readonly Guid LiveSessionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SessionTeamId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-000000000001");
    private static readonly Guid MissionStageId = Guid.Parse("dddddddd-dddd-dddd-dddd-000000000001");
    private static readonly Guid HintId = Guid.Parse("11111111-1111-1111-1111-000000000001");
    private static readonly DateTimeOffset ReleasedAt = new(2026, 6, 3, 14, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Manual")]
    [InlineData("Rule")]
    public void Create_Succeeds_ForSupportedUnlockReasons(string unlockReason)
    {
        var releasedHint = ReleasedHint.Create(
            LiveSessionId, SessionTeamId, MissionStageId, HintId, ReleasedAt, $"  {unlockReason}  ");

        Assert.NotEqual(Guid.Empty, releasedHint.Id);
        Assert.Equal(unlockReason, releasedHint.UnlockReason);
        Assert.Equal(HintId, releasedHint.HintId);
    }

    [Fact]
    public void Create_Throws_WhenLiveSessionIdEmpty()
        => AssertCreateThrows(
            "released_hint_live_session_required",
            () => ReleasedHint.Create(Guid.Empty, SessionTeamId, MissionStageId, HintId, ReleasedAt, "Manual"));

    [Fact]
    public void Create_Throws_WhenSessionTeamIdEmpty()
        => AssertCreateThrows(
            "released_hint_session_team_required",
            () => ReleasedHint.Create(LiveSessionId, Guid.Empty, MissionStageId, HintId, ReleasedAt, "Manual"));

    [Fact]
    public void Create_Throws_WhenMissionStageIdEmpty()
        => AssertCreateThrows(
            "released_hint_mission_stage_required",
            () => ReleasedHint.Create(LiveSessionId, SessionTeamId, Guid.Empty, HintId, ReleasedAt, "Manual"));

    [Fact]
    public void Create_Throws_WhenHintIdEmpty()
        => AssertCreateThrows(
            "released_hint_hint_required",
            () => ReleasedHint.Create(LiveSessionId, SessionTeamId, MissionStageId, Guid.Empty, ReleasedAt, "Manual"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Throws_WhenUnlockReasonBlank(string unlockReason)
        => AssertCreateThrows(
            "released_hint_unlock_reason_required",
            () => ReleasedHint.Create(LiveSessionId, SessionTeamId, MissionStageId, HintId, ReleasedAt, unlockReason));

    [Fact]
    public void Create_Throws_WhenUnlockReasonUnsupported()
        => AssertCreateThrows(
            "released_hint_unlock_reason_invalid",
            () => ReleasedHint.Create(LiveSessionId, SessionTeamId, MissionStageId, HintId, ReleasedAt, "Auto"));

    private static void AssertCreateThrows(string expectedCode, Func<ReleasedHint> act)
    {
        var exception = Assert.Throws<UmbralDomainException>(() => act());
        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(UmbralFailureCategory.Validation, exception.Category);
    }
}
