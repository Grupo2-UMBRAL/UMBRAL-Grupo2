using Xunit;
using System.Reflection;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Domain.LiveSessions;

namespace Umbral.SessionOperations.Api.Tests;

public sealed class EvidenceSubmissionQaTests
{
    private static readonly Guid TeamId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
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
    [InlineData(LiveSessionStates.Paused)]
    [InlineData(LiveSessionStates.Cancelled)]
    [InlineData(LiveSessionStates.Finalized)]
    public void SubmitEvidence_ThrowsBusinessError_WhenLiveSessionDoesNotAcceptEvidence(string blockedState)
    {
        var liveSession = CreateLiveSessionWithTeam(stageCount: 2);
        ForceState(liveSession, blockedState);

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
        Assert.Equal(LiveSessionStates.Finalized, liveSession.State);
        Assert.Equal(SessionTeamProgressStates.Completed, liveSession.GetProgressStateForTeam(TeamId));
        Assert.Null(liveSession.GetCurrentStageForTeam(TeamId));
        Assert.Equal(1, liveSession.SequenceNumber);
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
                    expectedQrHash: $"qr-stage-{stageOrder}"))
                .ToArray());

        liveSession.SessionTeams.Add(SessionTeam.Create(liveSession.Id, TeamId, "Alpha Team", NowUtc.AddMinutes(-20)));

        return liveSession;
    }

    private static void ForceState(LiveSession liveSession, string state)
    {
        var backingField = typeof(LiveSession).GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("LiveSession.State backing field was not found.");

        backingField.SetValue(liveSession, state);
    }
}


