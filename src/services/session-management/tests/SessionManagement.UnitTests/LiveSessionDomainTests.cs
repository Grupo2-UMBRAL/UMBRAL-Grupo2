using Umbral.ServiceDefaults;
using SessionManagement.Domain.LiveSessions;
using Xunit;

namespace SessionManagement.UnitTests;

// Direct guard/branch coverage for the LiveSession aggregate. Happy evidence/
// trivia/override flows already live in EvidenceSubmissionQaTests; this file
// targets the cold guard branches (state machine, enrollment, hints, stage
// deactivation) that integration QA flows never reach.
public sealed class LiveSessionCreateTests
{
    [Fact]
    public void Create_StartsScheduled_WithNormalizedContiguousStageFlow()
    {
        var liveSession = SampleLiveSessions.Create(SampleLiveSessions.TreasureStages(3));

        Assert.NotEqual(Guid.Empty, liveSession.Id);
        Assert.Equal("Scheduled", liveSession.State.Value);
        Assert.Equal(3, liveSession.SessionStageFlow.Count);
        Assert.Equal([1, 2, 3], liveSession.SessionStageFlow.Select(stage => stage.SessionStageOrder));
    }

    [Fact]
    public void Create_GeneratesId_WhenEmptyIdProvided()
    {
        var liveSession = LiveSession.Create(
            Guid.Empty,
            SampleLiveSessions.MissionId,
            "Mission",
            "Session",
            scheduledStartAtUtc: null,
            createdAtUtc: SampleLiveSessions.CreatedAt,
            sessionStageFlow: SampleLiveSessions.TreasureStages(1));

        Assert.NotEqual(Guid.Empty, liveSession.Id);
    }

    [Fact]
    public void Create_Throws_WhenMissionIdEmpty()
        => AssertCreateThrows("live_session_mission_required", missionId: Guid.Empty);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Throws_WhenMissionNameBlank(string missionName)
        => AssertCreateThrows("live_session_mission_name_required", missionName: missionName);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Throws_WhenNameBlank(string name)
        => AssertCreateThrows("live_session_name_required", name: name);

    [Fact]
    public void Create_Throws_WhenMissionNameTooLong()
        => AssertCreateThrows("live_session_mission_name_required_too_long", missionName: new string('m', 121));

    [Fact]
    public void Create_Throws_WhenNameTooLong()
        => AssertCreateThrows("live_session_name_required_too_long", name: new string('n', 121));

    [Fact]
    public void Create_Throws_WhenStageFlowEmpty()
        => AssertCreateThrows("live_session_stage_flow_required", stages: []);

    [Fact]
    public void Create_Throws_WhenStageFlowHasDuplicateMissionStage()
    {
        var duplicate = SampleLiveSessions.StageId(1);
        IReadOnlyList<LiveSessionStage> stages =
        [
            LiveSessionStage.Create(duplicate, "A", 1, 1, 10, "Easy", "Trivia", "P"),
            LiveSessionStage.Create(duplicate, "B", 2, 2, 10, "Easy", "Trivia", "P")
        ];

        AssertCreateThrows("live_session_stage_flow_duplicate_stage", stages: stages);
    }

    [Fact]
    public void Create_Throws_WhenStageFlowOrderNotContiguous()
    {
        IReadOnlyList<LiveSessionStage> stages =
        [
            SampleLiveSessions.TreasureStage(1),
            SampleLiveSessions.TreasureStage(3)
        ];

        AssertCreateThrows("live_session_stage_flow_order_invalid", stages: stages);
    }

    private static void AssertCreateThrows(
        string expectedCode,
        Guid? missionId = null,
        string missionName = "Mission",
        string name = "Session",
        IReadOnlyList<LiveSessionStage>? stages = null)
    {
        var exception = Assert.Throws<UmbralDomainException>(() => LiveSession.Create(
            Guid.NewGuid(),
            missionId ?? SampleLiveSessions.MissionId,
            missionName,
            name,
            scheduledStartAtUtc: null,
            createdAtUtc: SampleLiveSessions.CreatedAt,
            sessionStageFlow: stages ?? SampleLiveSessions.TreasureStages(2)));

        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(UmbralFailureCategory.Validation, exception.Category);
    }
}

public sealed class LiveSessionLifecycleTests
{
    [Fact]
    public void Start_Throws_WhenNotScheduled()
    {
        var liveSession = SampleLiveSessions.InState("Active");

        var exception = Assert.Throws<UmbralDomainException>(() => liveSession.Start(SampleLiveSessions.Now));
        Assert.Equal("live_session_cannot_start", exception.Code);
    }

    [Fact]
    public void Start_Throws_WhenNoSessionTeamsRegistered()
    {
        var liveSession = SampleLiveSessions.Create(SampleLiveSessions.TreasureStages(2));

        var exception = Assert.Throws<UmbralDomainException>(() => liveSession.Start(SampleLiveSessions.Now));
        Assert.Equal("live_session_requires_session_teams", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
    }

    [Fact]
    public void Start_AutoClosesOpenEnrollmentWindow_AndBecomesActive()
    {
        var liveSession = SampleLiveSessions.Create(SampleLiveSessions.TreasureStages(2));
        liveSession.AssignJoinCode(SampleLiveSessions.JoinCode());
        liveSession.OpenEnrollmentWindow(SampleLiveSessions.CreatedAt.AddMinutes(5));
        liveSession.RegisterTeam(
            SampleLiveSessions.TeamId(1), "Team 1", SampleLiveSessions.JoinCode(),
            SampleLiveSessions.CreatedAt.AddMinutes(10));
        Assert.Null(liveSession.EnrollmentWindowClosedAtUtc);

        liveSession.Start(SampleLiveSessions.CreatedAt.AddMinutes(20));

        Assert.Equal("Active", liveSession.State.Value);
        Assert.NotNull(liveSession.EnrollmentWindowClosedAtUtc);
    }

    [Fact]
    public void Start_BecomesActive_WhenTeamsPresentWithoutEnrollmentWindow()
    {
        var liveSession = SampleLiveSessions.WithTeams(1);

        liveSession.Start(SampleLiveSessions.Now);

        Assert.Equal("Active", liveSession.State.Value);
        Assert.Null(liveSession.EnrollmentWindowOpenedAtUtc);
    }

    [Fact]
    public void RegisterTeamByOperator_CreatesEmptyTeam_WithoutJoinCodeOrEnrollmentWindow()
    {
        var liveSession = SampleLiveSessions.Create(SampleLiveSessions.TreasureStages(2));

        var team = liveSession.RegisterTeamByOperator(Guid.NewGuid(), "Operator Team", SampleLiveSessions.Now);

        Assert.Equal("Operator Team", team.Name);
        Assert.Contains(liveSession.SessionTeams, existing => existing.Id == team.Id);
        Assert.Empty(liveSession.TeamParticipations);
    }

    [Fact]
    public void RegisterTeamByOperator_Throws_WhenTeamNameDuplicated()
    {
        var liveSession = SampleLiveSessions.Create(SampleLiveSessions.TreasureStages(2));
        liveSession.RegisterTeamByOperator(Guid.NewGuid(), "Duplicated", SampleLiveSessions.Now);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.RegisterTeamByOperator(Guid.NewGuid(), "duplicated", SampleLiveSessions.Now));

        Assert.Equal("session_team_name_duplicate", exception.Code);
    }

    [Fact]
    public void RegisterTeamByOperator_Throws_WhenNotScheduled()
    {
        var liveSession = SampleLiveSessions.InState("Active");

        Assert.Throws<UmbralDomainException>(() =>
            liveSession.RegisterTeamByOperator(Guid.NewGuid(), "Late Team", SampleLiveSessions.Now));
    }

    [Fact]
    public void Pause_TransitionsActiveToPaused()
    {
        var liveSession = SampleLiveSessions.InState("Active");

        liveSession.Pause();

        Assert.Equal("Paused", liveSession.State.Value);
    }

    [Fact]
    public void Pause_Throws_WhenNotActive()
    {
        var liveSession = SampleLiveSessions.WithTeams(1);

        var exception = Assert.Throws<UmbralDomainException>(liveSession.Pause);
        Assert.Equal("live_session_cannot_pause", exception.Code);
    }

    [Fact]
    public void Resume_TransitionsPausedToActive()
    {
        var liveSession = SampleLiveSessions.InState("Paused");

        liveSession.Resume();

        Assert.Equal("Active", liveSession.State.Value);
    }

    [Fact]
    public void Resume_Throws_WhenNotPaused()
    {
        var liveSession = SampleLiveSessions.InState("Active");

        var exception = Assert.Throws<UmbralDomainException>(liveSession.Resume);
        Assert.Equal("live_session_cannot_resume", exception.Code);
    }

    [Theory]
    [InlineData("Scheduled")]
    [InlineData("Active")]
    [InlineData("Paused")]
    public void Cancel_AllowedFromScheduledActiveOrPaused(string state)
    {
        var liveSession = SampleLiveSessions.InState(state);

        liveSession.Cancel();

        Assert.Equal("Canceled", liveSession.State.Value);
    }

    [Fact]
    public void Cancel_Throws_WhenFinalized()
    {
        var liveSession = SampleLiveSessions.InState("Finalized");

        var exception = Assert.Throws<UmbralDomainException>(liveSession.Cancel);
        Assert.Equal("live_session_cannot_cancel", exception.Code);
    }

    [Fact]
    public void FinalizeSession_IsNoOp_WhenAlreadyFinalized()
    {
        var liveSession = SampleLiveSessions.InState("Finalized");

        liveSession.FinalizeSession();

        Assert.Equal("Finalized", liveSession.State.Value);
    }

    [Fact]
    public void FinalizeSession_Throws_WhenScheduled()
    {
        var liveSession = SampleLiveSessions.WithTeams(1);

        var exception = Assert.Throws<UmbralDomainException>(liveSession.FinalizeSession);
        Assert.Equal("live_session_cannot_finalize", exception.Code);
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("Paused")]
    public void FinalizeSession_Finalizes_FromActiveOrPaused(string state)
    {
        var liveSession = SampleLiveSessions.InState(state);

        liveSession.FinalizeSession();

        Assert.Equal("Finalized", liveSession.State.Value);
    }

    [Fact]
    public void FinalizeAndRevealAllHints_ReleasesPendingHints_AndBumpsSequence()
    {
        IReadOnlyList<LiveSessionStage> stages =
        [
            SampleLiveSessions.TreasureStage(1, hints: [SampleLiveSessions.Hint(1)]),
            SampleLiveSessions.TreasureStage(2)
        ];
        var liveSession = SampleLiveSessions.InState("Active", teamCount: 1, stages: stages);

        var released = liveSession.FinalizeAndRevealAllHints(SampleLiveSessions.Now);

        Assert.Single(released);
        Assert.Equal("Finalized", liveSession.State.Value);
        Assert.Equal(1, liveSession.SequenceNumber);
    }

    [Fact]
    public void FinalizeAndRevealAllHints_SkipsAlreadyReleasedHints_AndDoesNotBumpSequenceAgain()
    {
        IReadOnlyList<LiveSessionStage> stages =
        [
            SampleLiveSessions.TreasureStage(1, hints: [SampleLiveSessions.Hint(1)]),
            SampleLiveSessions.TreasureStage(2)
        ];
        var liveSession = SampleLiveSessions.InState("Active", teamCount: 1, stages: stages);

        liveSession.FinalizeAndRevealAllHints(SampleLiveSessions.Now);
        var second = liveSession.FinalizeAndRevealAllHints(SampleLiveSessions.Now.AddMinutes(1));

        Assert.Empty(second);
        Assert.Equal(1, liveSession.SequenceNumber);
    }

    [Fact]
    public void FinalizeAndRevealAllHints_ReturnsEmpty_WhenNoStageHints()
    {
        var liveSession = SampleLiveSessions.InState("Active", stages: SampleLiveSessions.TreasureStages(2));

        var released = liveSession.FinalizeAndRevealAllHints(SampleLiveSessions.Now);

        Assert.Empty(released);
        Assert.Equal(0, liveSession.SequenceNumber);
    }
}

public sealed class LiveSessionEnrollmentTests
{
    [Fact]
    public void AssignJoinCode_Throws_WhenNull()
    {
        var liveSession = SampleLiveSessions.WithTeams(1);

        Assert.Throws<ArgumentNullException>(() => liveSession.AssignJoinCode(null!));
    }

    [Fact]
    public void AssignJoinCode_SetsValue_OnFirstAssignment()
    {
        var liveSession = SampleLiveSessions.WithTeams(1);

        liveSession.AssignJoinCode(SampleLiveSessions.JoinCode());

        Assert.Equal(SampleLiveSessions.JoinCodeText, liveSession.JoinCodeValue);
    }

    [Fact]
    public void AssignJoinCode_IsNoOp_WhenSameValueReassigned()
    {
        var liveSession = SampleLiveSessions.WithTeams(1);
        liveSession.AssignJoinCode(SampleLiveSessions.JoinCode());

        liveSession.AssignJoinCode(SampleLiveSessions.JoinCode());

        Assert.Equal(SampleLiveSessions.JoinCodeText, liveSession.JoinCodeValue);
    }

    [Fact]
    public void AssignJoinCode_Throws_WhenDifferentValueAlreadyAssigned()
    {
        var liveSession = SampleLiveSessions.WithTeams(1);
        liveSession.AssignJoinCode(SampleLiveSessions.JoinCode());

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.AssignJoinCode(JoinCode.Parse("WXYZ34")));
        Assert.Equal("live_session_join_code_already_assigned", exception.Code);
    }

    [Fact]
    public void OpenEnrollmentWindow_Throws_WhenNotScheduled()
    {
        var liveSession = SampleLiveSessions.InState("Active");

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.OpenEnrollmentWindow(SampleLiveSessions.Now));
        Assert.Equal("live_session_not_scheduled", exception.Code);
    }

    [Fact]
    public void OpenEnrollmentWindow_Throws_WhenJoinCodeMissing()
    {
        var liveSession = SampleLiveSessions.WithTeams(1);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.OpenEnrollmentWindow(SampleLiveSessions.Now));
        Assert.Equal("live_session_join_code_required_before_enrollment", exception.Code);
        Assert.Equal(UmbralFailureCategory.Validation, exception.Category);
    }

    [Fact]
    public void OpenEnrollmentWindow_Throws_WhenAlreadyClosed()
    {
        var liveSession = SampleLiveSessions.WithTeams(1);
        liveSession.AssignJoinCode(SampleLiveSessions.JoinCode());
        liveSession.OpenEnrollmentWindow(SampleLiveSessions.CreatedAt.AddMinutes(5));
        liveSession.CloseEnrollmentWindow(SampleLiveSessions.CreatedAt.AddMinutes(10));

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.OpenEnrollmentWindow(SampleLiveSessions.CreatedAt.AddMinutes(15)));
        Assert.Equal("live_session_enrollment_window_closed", exception.Code);
    }

    [Fact]
    public void OpenEnrollmentWindow_IsIdempotent_WhenAlreadyOpened()
    {
        var liveSession = SampleLiveSessions.WithTeams(1);
        liveSession.AssignJoinCode(SampleLiveSessions.JoinCode());
        var openedAt = SampleLiveSessions.CreatedAt.AddMinutes(5);
        liveSession.OpenEnrollmentWindow(openedAt);

        liveSession.OpenEnrollmentWindow(SampleLiveSessions.CreatedAt.AddMinutes(8));

        Assert.Equal(openedAt, liveSession.EnrollmentWindowOpenedAtUtc);
    }

    [Fact]
    public void CloseEnrollmentWindow_Throws_WhenNeverOpened()
    {
        var liveSession = SampleLiveSessions.WithTeams(1);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.CloseEnrollmentWindow(SampleLiveSessions.Now));
        Assert.Equal("live_session_enrollment_window_not_opened", exception.Code);
    }

    [Fact]
    public void CloseEnrollmentWindow_IsNoOp_WhenAlreadyClosed()
    {
        var liveSession = SampleLiveSessions.WithTeams(1);
        liveSession.AssignJoinCode(SampleLiveSessions.JoinCode());
        liveSession.OpenEnrollmentWindow(SampleLiveSessions.CreatedAt.AddMinutes(5));
        var closedAt = SampleLiveSessions.CreatedAt.AddMinutes(10);
        liveSession.CloseEnrollmentWindow(closedAt);

        liveSession.CloseEnrollmentWindow(SampleLiveSessions.CreatedAt.AddMinutes(15));

        Assert.Equal(closedAt, liveSession.EnrollmentWindowClosedAtUtc);
    }

    [Fact]
    public void CloseEnrollmentWindow_Throws_WhenCloseBeforeOpen()
    {
        var liveSession = SampleLiveSessions.WithTeams(1);
        liveSession.AssignJoinCode(SampleLiveSessions.JoinCode());
        liveSession.OpenEnrollmentWindow(SampleLiveSessions.CreatedAt.AddMinutes(10));

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.CloseEnrollmentWindow(SampleLiveSessions.CreatedAt.AddMinutes(5)));
        Assert.Equal("live_session_enrollment_window_close_time_invalid", exception.Code);
    }

    [Fact]
    public void RegisterTeam_Throws_WhenJoinCodeNotGenerated()
    {
        var liveSession = SampleLiveSessions.Create(SampleLiveSessions.TreasureStages(2));

        var exception = Assert.Throws<UmbralDomainException>(() => liveSession.RegisterTeam(
            SampleLiveSessions.TeamId(1), "Team 1", SampleLiveSessions.JoinCode(), SampleLiveSessions.Now));
        Assert.Equal("live_session_join_code_not_generated", exception.Code);
    }

    [Fact]
    public void RegisterTeam_Throws_WhenJoinCodeDoesNotMatch()
    {
        var liveSession = OpenForEnrollment();

        var exception = Assert.Throws<UmbralDomainException>(() => liveSession.RegisterTeam(
            SampleLiveSessions.TeamId(1), "Team 1", JoinCode.Parse("WXYZ34"), SampleLiveSessions.Now));
        Assert.Equal("join_code_invalid_for_live_session", exception.Code);
        Assert.Equal(UmbralFailureCategory.NotFound, exception.Category);
    }

    [Fact]
    public void RegisterTeam_Throws_WhenEnrollmentWindowNotOpen()
    {
        var liveSession = SampleLiveSessions.Create(SampleLiveSessions.TreasureStages(2));
        liveSession.AssignJoinCode(SampleLiveSessions.JoinCode());

        var exception = Assert.Throws<UmbralDomainException>(() => liveSession.RegisterTeam(
            SampleLiveSessions.TeamId(1), "Team 1", SampleLiveSessions.JoinCode(), SampleLiveSessions.Now));
        Assert.Equal("live_session_enrollment_window_not_active", exception.Code);
    }

    [Fact]
    public void RegisterTeam_Throws_WhenTeamNameDuplicated()
    {
        var liveSession = OpenForEnrollment();
        liveSession.RegisterTeam(
            SampleLiveSessions.TeamId(1), "Team Alpha", SampleLiveSessions.JoinCode(),
            SampleLiveSessions.CreatedAt.AddMinutes(6));

        var exception = Assert.Throws<UmbralDomainException>(() => liveSession.RegisterTeam(
            SampleLiveSessions.TeamId(2), "team alpha", SampleLiveSessions.JoinCode(),
            SampleLiveSessions.CreatedAt.AddMinutes(7)));
        Assert.Equal("session_team_name_duplicate", exception.Code);
    }

    [Fact]
    public void RegisterTeam_AddsTeamAndParticipation_OnHappyPath()
    {
        var liveSession = OpenForEnrollment();

        var team = liveSession.RegisterTeam(
            SampleLiveSessions.TeamId(1), "Team Alpha", SampleLiveSessions.JoinCode(),
            SampleLiveSessions.CreatedAt.AddMinutes(6));
        liveSession.EnrollParticipantInTeam(
            team.Id, "p1", SampleLiveSessions.JoinCode(), SampleLiveSessions.CreatedAt.AddMinutes(6));

        Assert.Single(liveSession.SessionTeams);
        Assert.Single(liveSession.TeamParticipations);
        Assert.Equal(team.Id, liveSession.TeamParticipations.Single().SessionTeamId);
    }

    [Fact]
    public void EnrollParticipantInTeam_Throws_WhenTeamNotInSession()
    {
        var liveSession = OpenForEnrollment();

        var exception = Assert.Throws<UmbralDomainException>(() => liveSession.EnrollParticipantInTeam(
            Guid.NewGuid(), "p1", SampleLiveSessions.JoinCode(), SampleLiveSessions.Now));
        Assert.Equal("session_team_not_found", exception.Code);
    }

    [Fact]
    public void EnrollParticipantInTeam_ReturnsExisting_WhenSameParticipantSameTeam()
    {
        var liveSession = OpenForEnrollment();
        var team = liveSession.RegisterTeam(
            SampleLiveSessions.TeamId(1), "Team Alpha", SampleLiveSessions.JoinCode(),
            SampleLiveSessions.CreatedAt.AddMinutes(6));
        liveSession.EnrollParticipantInTeam(
            team.Id, "p1", SampleLiveSessions.JoinCode(), SampleLiveSessions.CreatedAt.AddMinutes(6));

        liveSession.EnrollParticipantInTeam(
            team.Id, "p1", SampleLiveSessions.JoinCode(), SampleLiveSessions.CreatedAt.AddMinutes(7));

        Assert.Single(liveSession.TeamParticipations);
    }

    [Fact]
    public void EnrollParticipantInTeam_MovesParticipant_WhenAlreadyOnAnotherTeam()
    {
        var liveSession = OpenForEnrollment();
        var teamAlpha = liveSession.RegisterTeam(
            SampleLiveSessions.TeamId(1), "Team Alpha", SampleLiveSessions.JoinCode(),
            SampleLiveSessions.CreatedAt.AddMinutes(6));
        liveSession.EnrollParticipantInTeam(
            teamAlpha.Id, "p1", SampleLiveSessions.JoinCode(), SampleLiveSessions.CreatedAt.AddMinutes(6));
        var teamBravo = liveSession.RegisterTeam(
            SampleLiveSessions.TeamId(2), "Team Bravo", SampleLiveSessions.JoinCode(),
            SampleLiveSessions.CreatedAt.AddMinutes(7));

        liveSession.EnrollParticipantInTeam(
            teamBravo.Id, "p1", SampleLiveSessions.JoinCode(), SampleLiveSessions.CreatedAt.AddMinutes(8));

        var participation = liveSession.TeamParticipations.Single(p => p.ParticipantUserId == "p1");
        Assert.Equal(teamBravo.Id, participation.SessionTeamId);
    }

    private static LiveSession OpenForEnrollment()
    {
        var liveSession = SampleLiveSessions.Create(SampleLiveSessions.TreasureStages(2));
        liveSession.AssignJoinCode(SampleLiveSessions.JoinCode());
        liveSession.OpenEnrollmentWindow(SampleLiveSessions.CreatedAt.AddMinutes(5));
        return liveSession;
    }
}

public sealed class LiveSessionEvidenceGuardTests
{
    private static readonly Guid Team = SampleLiveSessions.TeamId(1);

    [Fact]
    public void SubmitEvidence_Throws_WhenSessionTeamNotInSession()
    {
        var liveSession = SampleLiveSessions.WithTeams(1);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.SubmitEvidence(Guid.NewGuid(), "qr-stage-1", SampleLiveSessions.Now));
        Assert.Equal("session_team_not_found", exception.Code);
    }

    [Fact]
    public void SubmitEvidence_Throws_WhenTeamProgressAlreadyCompleted()
    {
        var liveSession = SampleLiveSessions.WithTeams(1, SampleLiveSessions.TreasureStages(2));
        var progress = SessionTeamProgress.Create(liveSession.Id, Team, SampleLiveSessions.Now);
        progress.Complete(SampleLiveSessions.Now);
        liveSession.TeamProgressions.Add(progress);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.SubmitEvidence(Team, "qr-stage-1", SampleLiveSessions.Now));
        Assert.Equal("session_team_progress_already_completed", exception.Code);
    }

    [Fact]
    public void SubmitEvidence_Throws_WhenProgressStageIndexOutsideFlow()
    {
        var liveSession = SampleLiveSessions.WithTeams(1, SampleLiveSessions.TreasureStages(2));
        var progress = SessionTeamProgress.Create(liveSession.Id, Team, SampleLiveSessions.Now);
        progress.AdvanceTo(5, SampleLiveSessions.Now);
        liveSession.TeamProgressions.Add(progress);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.SubmitEvidence(Team, "qr-stage-1", SampleLiveSessions.Now));
        Assert.Equal("session_team_progress_stage_index_invalid", exception.Code);
    }

    [Fact]
    public void SubmitEvidence_Throws_WhenCurrentStageIsNotTreasureHunt()
    {
        var liveSession = SampleLiveSessions.WithTeams(1, SampleLiveSessions.TriviaStages(2));

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.SubmitEvidence(Team, "qr-stage-1", SampleLiveSessions.Now));
        Assert.Equal("evidence_submission_stage_not_treasure_hunt", exception.Code);
    }

    [Fact]
    public void SubmitEvidence_Throws_WhenStageHasNoExpectedHash()
    {
        IReadOnlyList<LiveSessionStage> stages =
        [
            LiveSessionStage.Create(SampleLiveSessions.StageId(1), "S1", 1, 1, 10, "Easy", "TreasureHunt", "P", expectedQrHash: null),
            SampleLiveSessions.TreasureStage(2)
        ];
        var liveSession = SampleLiveSessions.WithTeams(1, stages);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.SubmitEvidence(Team, "qr-stage-1", SampleLiveSessions.Now));
        Assert.Equal("evidence_submission_expected_qr_hash_required", exception.Code);
    }

    [Fact]
    public void SubmitEvidence_Throws_WhenStageAlreadyResolvedByTeam()
    {
        var liveSession = SampleLiveSessions.WithTeams(1, SampleLiveSessions.TreasureStages(2));
        var stage = liveSession.SessionStageFlow[0];
        liveSession.EvidenceSubmissions.Add(EvidenceSubmission.CreateTreasureHunt(
            liveSession.Id, Team, stage, "qr-stage-1", ValidationOutcome.Accepted, null, SampleLiveSessions.Now));

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.SubmitEvidence(Team, "qr-stage-1", SampleLiveSessions.Now));
        Assert.Equal("session_stage_already_resolved_by_team", exception.Code);
    }

    [Fact]
    public void SubmitTriviaAnswer_Throws_WhenCurrentStageIsNotTrivia()
    {
        var liveSession = SampleLiveSessions.WithTeams(1, SampleLiveSessions.TreasureStages(2));

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.SubmitTriviaAnswer(Team, SampleLiveSessions.CorrectChoiceId, SampleLiveSessions.Now));
        Assert.Equal("evidence_submission_stage_not_trivia", exception.Code);
    }

    [Fact]
    public void SubmitTriviaAnswer_Throws_WhenStageHasNoCorrectChoice()
    {
        IReadOnlyList<LiveSessionStage> stages =
        [
            SampleLiveSessions.TriviaStage(1, withCorrectChoice: false),
            SampleLiveSessions.TriviaStage(2)
        ];
        var liveSession = SampleLiveSessions.WithTeams(1, stages);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.SubmitTriviaAnswer(Team, SampleLiveSessions.CorrectChoiceId, SampleLiveSessions.Now));
        Assert.Equal("evidence_submission_trivia_answer_required", exception.Code);
    }
}

public sealed class LiveSessionHintTests
{
    private static readonly Guid Team = SampleLiveSessions.TeamId(1);

    private static IReadOnlyList<LiveSessionStage> StagesWithHint() =>
    [
        SampleLiveSessions.TreasureStage(1, hints: [SampleLiveSessions.Hint(1)]),
        SampleLiveSessions.TreasureStage(2)
    ];

    [Theory]
    [InlineData("Scheduled")]
    [InlineData("Finalized")]
    [InlineData("Canceled")]
    public void ReleaseHint_Throws_WhenSessionNotActiveOrPaused(string state)
    {
        var liveSession = SampleLiveSessions.InState(state, 1, StagesWithHint());

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.ReleaseHint(Team, SampleLiveSessions.HintId(1), SampleLiveSessions.Now));
        Assert.Equal("live_session_not_accepting_hint_release", exception.Code);
    }

    [Fact]
    public void ReleaseHint_Throws_WhenTeamHasNoCurrentStage()
    {
        var liveSession = SampleLiveSessions.InState("Active", 1, StagesWithHint());
        var progress = SessionTeamProgress.Create(liveSession.Id, Team, SampleLiveSessions.Now);
        progress.Complete(SampleLiveSessions.Now);
        liveSession.TeamProgressions.Add(progress);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.ReleaseHint(Team, SampleLiveSessions.HintId(1), SampleLiveSessions.Now));
        Assert.Equal("released_hint_current_stage_required", exception.Code);
    }

    [Fact]
    public void ReleaseHint_Throws_WhenHintNotInCurrentStage()
    {
        var liveSession = SampleLiveSessions.InState("Active", 1, StagesWithHint());

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.ReleaseHint(Team, Guid.NewGuid(), SampleLiveSessions.Now));
        Assert.Equal("released_hint_not_in_current_stage", exception.Code);
    }

    [Fact]
    public void ReleaseHint_Throws_WhenHintAlreadyReleased()
    {
        var liveSession = SampleLiveSessions.InState("Active", 1, StagesWithHint());
        liveSession.ReleaseHint(Team, SampleLiveSessions.HintId(1), SampleLiveSessions.Now);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.ReleaseHint(Team, SampleLiveSessions.HintId(1), SampleLiveSessions.Now.AddMinutes(1)));
        Assert.Equal("released_hint_duplicate", exception.Code);
    }

    [Fact]
    public void ReleaseHint_ReleasesHint_OnHappyPath()
    {
        var liveSession = SampleLiveSessions.InState("Active", 1, StagesWithHint());

        var released = liveSession.ReleaseHint(Team, SampleLiveSessions.HintId(1), SampleLiveSessions.Now);

        Assert.Equal(SampleLiveSessions.HintId(1), released.HintId);
        Assert.Single(liveSession.ReleasedHints);
        Assert.Equal(1, liveSession.SequenceNumber);
    }

    [Fact]
    public void AddOperationalHint_Throws_WhenSessionNotAcceptingHints()
    {
        var liveSession = SampleLiveSessions.WithTeams(1, SampleLiveSessions.TreasureStages(2));

        var exception = Assert.Throws<UmbralDomainException>(() => liveSession.AddOperationalHint(
            SampleLiveSessions.StageId(1), "Look north", null, null, SampleLiveSessions.Now));
        Assert.Equal("live_session_not_accepting_hint_release", exception.Code);
    }

    [Fact]
    public void AddOperationalHint_Throws_WhenStageNotInFlow()
    {
        var liveSession = SampleLiveSessions.InState("Active", 1, SampleLiveSessions.TreasureStages(2));

        var exception = Assert.Throws<UmbralDomainException>(() => liveSession.AddOperationalHint(
            Guid.NewGuid(), "Look north", null, null, SampleLiveSessions.Now));
        Assert.Equal("operational_hint_stage_not_in_flow", exception.Code);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AddOperationalHint_Throws_WhenLatitudeNotFinite(double latitude)
    {
        var liveSession = SampleLiveSessions.InState("Active", 1, SampleLiveSessions.TreasureStages(2));

        var exception = Assert.Throws<UmbralDomainException>(() => liveSession.AddOperationalHint(
            SampleLiveSessions.StageId(1), "Look north", latitude, 10.0, SampleLiveSessions.Now));
        Assert.Equal("operational_hint_latitude_invalid", exception.Code);
    }

    [Fact]
    public void AddOperationalHint_Throws_WhenLongitudeNotFinite()
    {
        var liveSession = SampleLiveSessions.InState("Active", 1, SampleLiveSessions.TreasureStages(2));

        var exception = Assert.Throws<UmbralDomainException>(() => liveSession.AddOperationalHint(
            SampleLiveSessions.StageId(1), "Look north", 10.0, double.NegativeInfinity, SampleLiveSessions.Now));
        Assert.Equal("operational_hint_longitude_invalid", exception.Code);
    }

    [Fact]
    public void AddOperationalHint_AddsHintWithoutCoordinates_OnHappyPath()
    {
        var liveSession = SampleLiveSessions.InState("Active", 1, SampleLiveSessions.TreasureStages(2));

        var hint = liveSession.AddOperationalHint(
            SampleLiveSessions.StageId(1), "Look north", null, null, SampleLiveSessions.Now);

        Assert.Null(hint.Latitude);
        Assert.Null(hint.Longitude);
        Assert.Contains(liveSession.SessionStageFlow[0].Hints, h => h.Id == hint.Id);
        Assert.Equal(1, liveSession.SequenceNumber);
    }

    [Fact]
    public void AddOperationalHint_StoresCoordinates_WhenProvided()
    {
        var liveSession = SampleLiveSessions.InState("Active", 1, SampleLiveSessions.TreasureStages(2));

        var hint = liveSession.AddOperationalHint(
            SampleLiveSessions.StageId(1), "Look north", 10.5, 20.25, SampleLiveSessions.Now);

        Assert.Equal(10.5m, hint.Latitude);
        Assert.Equal(20.25m, hint.Longitude);
    }
}

public sealed class LiveSessionStageDeactivationTests
{
    private static readonly Guid Team = SampleLiveSessions.TeamId(1);

    [Fact]
    public void IsStagePending_Throws_WhenStageNotInFlow()
    {
        var liveSession = SampleLiveSessions.WithTeams(1, SampleLiveSessions.TreasureStages(2));

        var exception = Assert.Throws<UmbralDomainException>(() => liveSession.IsStagePending(Guid.NewGuid()));
        Assert.Equal("session_stage_not_in_flow", exception.Code);
    }

    [Fact]
    public void IsStagePending_True_WhenNoTeamHasPassedStage()
    {
        var liveSession = SampleLiveSessions.WithTeams(1, SampleLiveSessions.TreasureStages(2));

        Assert.True(liveSession.IsStagePending(SampleLiveSessions.StageId(1)));
    }

    [Fact]
    public void GetCurrentStageForTeam_ReturnsNull_WhenProgressIndexBeyondFlow()
    {
        var liveSession = SampleLiveSessions.WithTeams(1, SampleLiveSessions.TreasureStages(2));
        var progress = SessionTeamProgress.Create(liveSession.Id, Team, SampleLiveSessions.Now);
        progress.AdvanceTo(5, SampleLiveSessions.Now);
        liveSession.TeamProgressions.Add(progress);

        Assert.Null(liveSession.GetCurrentStageForTeam(Team));
    }

    [Fact]
    public void DeactivateStage_Throws_WhenStateDoesNotAllowIt()
    {
        var liveSession = SampleLiveSessions.InState("Finalized", 1, SampleLiveSessions.TreasureStages(2));

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.DeactivateStage(SampleLiveSessions.StageId(1), SampleLiveSessions.Now));
        Assert.Equal("live_session_stage_deactivation_not_allowed_for_state", exception.Code);
    }

    [Fact]
    public void DeactivateStage_Throws_WhenOnlyOneStageRemains()
    {
        var liveSession = SampleLiveSessions.WithTeams(1, SampleLiveSessions.TreasureStages(1));

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.DeactivateStage(SampleLiveSessions.StageId(1), SampleLiveSessions.Now));
        Assert.Equal("session_stage_flow_last_pending_stage", exception.Code);
    }

    [Fact]
    public void DeactivateStage_Throws_WhenStageNotInFlow()
    {
        var liveSession = SampleLiveSessions.WithTeams(1, SampleLiveSessions.TreasureStages(2));

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.DeactivateStage(Guid.NewGuid(), SampleLiveSessions.Now));
        Assert.Equal("session_stage_not_in_flow", exception.Code);
    }

    [Fact]
    public void DeactivateStage_Throws_WhenStageNotPending()
    {
        var liveSession = SampleLiveSessions.WithTeams(1, SampleLiveSessions.TreasureStages(2));
        liveSession.SubmitEvidence(Team, "qr-stage-1", SampleLiveSessions.Now); // accept stage 1, advance team past it

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.DeactivateStage(SampleLiveSessions.StageId(1), SampleLiveSessions.Now));
        Assert.Equal("session_stage_not_pending", exception.Code);
    }

    [Fact]
    public void DeactivateStage_Throws_WhenRemovalLeavesNoPendingStage()
    {
        var liveSession = SampleLiveSessions.WithTeams(1, SampleLiveSessions.TreasureStages(2));
        liveSession.SubmitEvidence(Team, "qr-stage-1", SampleLiveSessions.Now); // team now on stage 2; stage 1 resolved

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.DeactivateStage(SampleLiveSessions.StageId(2), SampleLiveSessions.Now));
        Assert.Equal("session_stage_flow_last_pending_stage", exception.Code);
    }

    [Fact]
    public void DeactivateStage_RemovesStageAndReindexes_OnHappyPath()
    {
        var liveSession = SampleLiveSessions.WithTeams(1, SampleLiveSessions.TreasureStages(3));

        liveSession.DeactivateStage(SampleLiveSessions.StageId(2), SampleLiveSessions.Now);

        Assert.Equal(2, liveSession.SessionStageFlow.Count);
        Assert.DoesNotContain(liveSession.SessionStageFlow, stage => stage.MissionStageId == SampleLiveSessions.StageId(2));
        Assert.Equal([1, 2], liveSession.SessionStageFlow.Select(stage => stage.SessionStageOrder));
        Assert.Equal(1, liveSession.SequenceNumber);
    }

    [Fact]
    public void DeactivateStage_ShiftsTeamProgress_WhenRemovingStageBeforeCurrent()
    {
        var liveSession = SampleLiveSessions.WithTeams(1, SampleLiveSessions.TreasureStages(3));
        liveSession.SubmitEvidence(Team, "qr-stage-1", SampleLiveSessions.Now); // team -> index 1 (stage 2)

        liveSession.DeactivateStage(SampleLiveSessions.StageId(2), SampleLiveSessions.Now); // remove current; advance to reindexed slot

        Assert.Equal("Stage 3", liveSession.GetCurrentStageForTeam(Team)?.Name);
    }
}

public sealed class LiveSessionOverrideGuardTests
{
    private static readonly Guid Team = SampleLiveSessions.TeamId(1);

    [Fact]
    public void OverrideValidationOutcome_Throws_WhenSubmissionNotFound()
    {
        var liveSession = SampleLiveSessions.WithTeams(1, SampleLiveSessions.TreasureStages(2));

        var exception = Assert.Throws<UmbralDomainException>(() => liveSession.OverrideValidationOutcome(
            Guid.NewGuid(), "operator-1", isAccepted: true, "reason", SampleLiveSessions.Now));
        Assert.Equal("evidence_submission_not_found", exception.Code);
        Assert.Equal(UmbralFailureCategory.NotFound, exception.Category);
    }

    [Fact]
    public void OverrideValidationOutcome_Throws_WhenAcceptedStageNoLongerInFlow()
    {
        var liveSession = SampleLiveSessions.WithTeams(1, SampleLiveSessions.TreasureStages(3));
        var rejected = liveSession.SubmitEvidence(Team, "wrong", SampleLiveSessions.Now); // reject on stage 1
        liveSession.DeactivateStage(SampleLiveSessions.StageId(1), SampleLiveSessions.Now); // remove that stage

        var exception = Assert.Throws<UmbralDomainException>(() => liveSession.OverrideValidationOutcome(
            rejected.Id, "operator-1", isAccepted: true, "reason", SampleLiveSessions.Now.AddMinutes(1)));
        Assert.Equal("validation_override_stage_not_in_flow", exception.Code);
    }

    [Fact]
    public void OverrideValidationOutcome_RejectsToRejected_WithoutStageAdvance()
    {
        var liveSession = SampleLiveSessions.WithTeams(1, SampleLiveSessions.TreasureStages(2));
        var rejected = liveSession.SubmitEvidence(Team, "wrong", SampleLiveSessions.Now);

        var overrideLog = liveSession.OverrideValidationOutcome(
            rejected.Id, "operator-1", isAccepted: false, "still wrong", SampleLiveSessions.Now.AddMinutes(1));

        Assert.Equal(ValidationOutcome.Rejected, overrideLog.NewOutcome);
        Assert.Equal("Stage 1", liveSession.GetCurrentStageForTeam(Team)?.Name);
    }
}
