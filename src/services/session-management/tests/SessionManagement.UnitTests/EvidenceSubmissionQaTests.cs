using Xunit;
using System.Reflection;
using Umbral.ServiceDefaults;
using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.UnitTests;

public sealed class EvidenceSubmissionQaTests
{
    private static readonly Guid TeamId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CorrectChoiceId = Guid.Parse("c0c0c0c0-0000-0000-0000-000000000001");
    private static readonly Guid WrongChoiceId = Guid.Parse("c0c0c0c0-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset NowUtc = new(2026, 6, 3, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SubmitEvidence_AcceptsMatchingQrHash_AndAdvancesTeamToNextStage()
    {
        var liveSession = CreateLiveSessionWithTeam(stageCount: 2);

        var submission = liveSession.SubmitEvidence(TeamId, " QR-STAGE-1 ", NowUtc);

        Assert.Equal(ValidationOutcome.Accepted, submission.Outcome);
        Assert.Equal("qr-stage-1", submission.SubmittedHash, ignoreCase: true);
        Assert.Equal(SessionTeamProgressStates.InProgress, liveSession.GetProgressStateForTeam(TeamId));
        Assert.Equal("Stage 2", liveSession.GetCurrentStageForTeam(TeamId)?.Name);
        Assert.Equal(1, liveSession.SequenceNumber);
        Assert.Single(liveSession.EvidenceSubmissions);
    }

    [Fact]
    public void SubmitEvidence_RejectsMismatchingQrHash_AndKeepsTeamOnCurrentStage()
    {
        var liveSession = CreateLiveSessionWithTeam(stageCount: 2);

        var submission = liveSession.SubmitEvidence(TeamId, "wrong-hash", NowUtc);

        Assert.Equal(ValidationOutcome.Rejected, submission.Outcome);
        Assert.Equal("qr_hash_mismatch", submission.FailureReason);
        Assert.Equal(SessionTeamProgressStates.InProgress, liveSession.GetProgressStateForTeam(TeamId));
        Assert.Equal("Stage 1", liveSession.GetCurrentStageForTeam(TeamId)?.Name);
        Assert.Equal(1, liveSession.SequenceNumber);
        Assert.Single(liveSession.EvidenceSubmissions);
    }

    [Theory]
    [InlineData("Paused")]
    [InlineData("Canceled")]
    [InlineData("Finalized")]
    public void SubmitEvidence_ThrowsBusinessError_WhenLiveSessionDoesNotAcceptEvidence(string blockedState)
    {
        var liveSession = CreateLiveSessionWithTeam(stageCount: 2);
        SampleLiveSessions.ForceState(liveSession, blockedState);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.SubmitEvidence(TeamId, "qr-stage-1", NowUtc));

        Assert.Equal("live_session_not_accepting_evidence", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
        Assert.Empty(liveSession.EvidenceSubmissions);
    }

    [Fact]
    public void SubmitEvidence_FinalizesLiveSession_WhenLastStageIsAccepted()
    {
        var liveSession = CreateLiveSessionWithTeam(stageCount: 1);

        var submission = liveSession.SubmitEvidence(TeamId, "qr-stage-1", NowUtc);

        Assert.Equal(ValidationOutcome.Accepted, submission.Outcome);
        Assert.Equal("Finalized", liveSession.State.Value);
        Assert.Equal(SessionTeamProgressStates.Completed, liveSession.GetProgressStateForTeam(TeamId));
        Assert.Null(liveSession.GetCurrentStageForTeam(TeamId));
        Assert.Equal(1, liveSession.SequenceNumber);
    }

    [Fact]
    public void SubmitTriviaAnswer_AcceptsCorrectChoice_AndAdvancesTeamToNextStage()
    {
        var liveSession = CreateTriviaLiveSessionWithTeam(stageCount: 2);

        var submission = liveSession.SubmitTriviaAnswer(TeamId, CorrectChoiceId, NowUtc);

        Assert.Equal(ValidationOutcome.Accepted, submission.Outcome);
        Assert.Equal(CorrectChoiceId, submission.SubmittedChoiceId);
        Assert.Null(submission.SubmittedText);
        Assert.Equal(SessionTeamProgressStates.InProgress, liveSession.GetProgressStateForTeam(TeamId));
        Assert.Equal("Trivia Stage 2", liveSession.GetCurrentStageForTeam(TeamId)?.Name);
        Assert.Equal(1, liveSession.SequenceNumber);
    }

    [Fact]
    public void SubmitTriviaAnswer_RejectsWrongChoice_AndKeepsTeamOnCurrentStage()
    {
        var liveSession = CreateTriviaLiveSessionWithTeam(stageCount: 2);

        var submission = liveSession.SubmitTriviaAnswer(TeamId, WrongChoiceId, NowUtc);

        Assert.Equal(ValidationOutcome.Rejected, submission.Outcome);
        Assert.Equal(WrongChoiceId, submission.SubmittedChoiceId);
        Assert.Equal("trivia_answer_mismatch", submission.FailureReason);
        Assert.Equal(SessionTeamProgressStates.InProgress, liveSession.GetProgressStateForTeam(TeamId));
        Assert.Equal("Trivia Stage 1", liveSession.GetCurrentStageForTeam(TeamId)?.Name);
        Assert.Equal(1, liveSession.SequenceNumber);
    }

    [Fact]
    public void SubmitTriviaAnswer_ThrowsValidation_WhenChoiceNotInPlay()
    {
        var liveSession = CreateTriviaLiveSessionWithTeam(stageCount: 2);

        var exception = Assert.Throws<UmbralDomainException>(() =>
            liveSession.SubmitTriviaAnswer(TeamId, Guid.NewGuid(), NowUtc));

        Assert.Equal("evidence_submission_choice_not_in_play", exception.Code);
        Assert.Equal(UmbralFailureCategory.Validation, exception.Category);
        Assert.Empty(liveSession.EvidenceSubmissions);
    }

    [Fact]
    public void OverrideValidationOutcome_AcceptsRejectedTriviaSubmission_AndAdvancesTeam()
    {
        var liveSession = CreateTriviaLiveSessionWithTeam(stageCount: 2);
        var submission = liveSession.SubmitTriviaAnswer(TeamId, WrongChoiceId, NowUtc);

        var overrideLog = liveSession.OverrideValidationOutcome(
            submission.Id,
            "operator-1",
            isAccepted: true,
            "Respuesta equivalente aceptada por operador.",
            NowUtc.AddMinutes(1));

        Assert.Equal(ValidationOutcome.Accepted, submission.Outcome);
        Assert.Null(submission.FailureReason);
        Assert.Equal(ValidationOutcome.Rejected, overrideLog.PreviousOutcome);
        Assert.Equal(ValidationOutcome.Accepted, overrideLog.NewOutcome);
        Assert.Equal("operator-1", overrideLog.OperatorUserId);
        Assert.Equal("Respuesta equivalente aceptada por operador.", overrideLog.Reason);
        Assert.Equal(submission.Id, overrideLog.EvidenceSubmissionId);
        Assert.Equal(TeamId, overrideLog.SessionTeamId);
        Assert.Equal(submission.MissionStageId, overrideLog.MissionStageId);
        Assert.Equal("Trivia Stage 2", liveSession.GetCurrentStageForTeam(TeamId)?.Name);
        Assert.Equal(2, liveSession.SequenceNumber);
        Assert.Single(liveSession.ValidationOverrideLogs);
    }

    [Fact]
    public void OverrideValidationOutcome_DoesNotAdvanceAgain_WhenSubmissionWasAlreadyAccepted()
    {
        var liveSession = CreateTriviaLiveSessionWithTeam(stageCount: 3);
        var submission = liveSession.SubmitTriviaAnswer(TeamId, CorrectChoiceId, NowUtc);

        var overrideLog = liveSession.OverrideValidationOutcome(
            submission.Id,
            "operator-1",
            isAccepted: true,
            "Confirmacion administrativa.",
            NowUtc.AddMinutes(1));

        Assert.Equal(ValidationOutcome.Accepted, overrideLog.PreviousOutcome);
        Assert.Equal(ValidationOutcome.Accepted, overrideLog.NewOutcome);
        Assert.Equal("Trivia Stage 2", liveSession.GetCurrentStageForTeam(TeamId)?.Name);
        Assert.Equal(2, liveSession.SequenceNumber);
        Assert.Single(liveSession.ValidationOverrideLogs);
    }

    [Fact]
    public void SubmitTriviaAnswer_ResolvesAutomaticallyByDefault_WithoutPendingReviewState()
    {
        var liveSession = CreateTriviaLiveSessionWithTeam(stageCount: 2);

        var acceptedSubmission = liveSession.SubmitTriviaAnswer(TeamId, CorrectChoiceId, NowUtc);
        var rejectedSession = CreateTriviaLiveSessionWithTeam(stageCount: 2);
        var rejectedSubmission = rejectedSession.SubmitTriviaAnswer(TeamId, WrongChoiceId, NowUtc);

        Assert.Equal(ValidationOutcome.Accepted, acceptedSubmission.Outcome);
        Assert.Equal(ValidationOutcome.Rejected, rejectedSubmission.Outcome);
        Assert.DoesNotContain("Pending", Enum.GetNames<ValidationOutcome>());
        Assert.DoesNotContain("Review", acceptedSubmission.Outcome.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Review", rejectedSubmission.Outcome.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static LiveSession CreateLiveSessionWithTeam(int stageCount)
    {
        var liveSession = LiveSession.Create(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            "Treasure Mission",
            "Live Treasure Run",
            scheduledStartAtUtc: null,
            createdAtUtc: NowUtc.AddMinutes(-30),
            sessionStageFlow: Enumerable.Range(1, stageCount)
                .Select(stageOrder => LiveSessionStage.Create(
                    Guid.Parse($"dddddddd-dddd-dddd-dddd-{stageOrder:000000000000}"),
                    $"Stage {stageOrder}",
                    stageOrder,
                    stageOrder,
                    10,
                    "Medium",
                    "TreasureHunt",
                    $"Prompt for Stage {stageOrder}",
                    expectedQrHash: $"qr-stage-{stageOrder}"))
                .ToArray());

        liveSession.SessionTeams.Add(SessionTeam.Create(liveSession.Id, TeamId, "Alpha Team", NowUtc.AddMinutes(-20)));

        return liveSession;
    }

    private static LiveSession CreateTriviaLiveSessionWithTeam(int stageCount)
    {
        var liveSession = LiveSession.Create(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            "Trivia Mission",
            "Live Trivia Run",
            scheduledStartAtUtc: null,
            createdAtUtc: NowUtc.AddMinutes(-30),
            sessionStageFlow: Enumerable.Range(1, stageCount)
                .Select(stageOrder => LiveSessionStage.Create(
                    Guid.Parse($"eeeeeeee-eeee-eeee-eeee-{stageOrder:000000000000}"),
                    $"Trivia Stage {stageOrder}",
                    stageOrder,
                    stageOrder,
                    10,
                    "Medium",
                    "Trivia",
                    $"Prompt for Trivia Stage {stageOrder}",
                    choices:
                    [
                        LiveSessionChoice.Create(CorrectChoiceId, "Caracas"),
                        LiveSessionChoice.Create(WrongChoiceId, "Valencia")
                    ],
                    correctChoiceId: CorrectChoiceId))
                .ToArray());

        liveSession.SessionTeams.Add(SessionTeam.Create(liveSession.Id, TeamId, "Alpha Team", NowUtc.AddMinutes(-20)));

        return liveSession;
    }
}


