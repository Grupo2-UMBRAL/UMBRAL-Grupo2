using SessionManagement.Domain.LiveSessions.States;
using Moq;
using Umbral.ServiceDefaults;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Application.Abstractions;
using SessionManagement.Application.Abstractions.Realtime;
using SessionManagement.Application.Abstractions.Scoring;
using SessionManagement.Application.Features.SessionLifecycle;
using SessionManagement.Application.Features.SessionEnrollment;
using SessionManagement.Application.Features.EvidenceSubmissions;
using SessionManagement.Application.Features.Hints;
using Xunit;

namespace SessionManagement.UnitTests;

// Tier 1 handler tests. The aggregate guards themselves are proven in
// LiveSessionDomainTests; here we cover each handler's own orchestration
// branches: load-or-404, identity guards, scoring-audit / notifier fan-out,
// and the accepted-vs-rejected / finalize side effects.
file static class HandlerScaffold
{
    public const string Participant = "participant-1";

    public static readonly Guid Team = SampleLiveSessions.TeamId(1);

    public static Mock<ICurrentParticipantIdentity> Participants(string participantUserId)
    {
        var identity = new Mock<ICurrentParticipantIdentity>();
        identity.Setup(i => i.GetRequiredParticipantUserId()).Returns(ParticipantUserId.Parse(participantUserId));
        return identity;
    }

    public static Mock<ICurrentOperatorIdentity> Operator(string operatorUserId)
    {
        var identity = new Mock<ICurrentOperatorIdentity>();
        identity.Setup(i => i.GetRequiredOperatorUserId()).Returns(operatorUserId);
        return identity;
    }

    public static TimeProvider Clock() => new FixedTimeProvider(SampleLiveSessions.Now);
}

public sealed class OverrideValidationOutcomeHandlerTests
{
    [Fact]
    public async Task Handle_Throws_WhenSubmissionNotFound()
    {
        var session = SampleLiveSessions.EnrolledActive(SampleLiveSessions.TriviaStages(2));
        var handler = Build(session, out _, out _, out _);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(new OverrideValidationOutcomeCommand(Guid.NewGuid(), true, "reason"), default));
        Assert.Equal("evidence_submission_not_found", exception.Code);
    }

    [Fact]
    public async Task Handle_AcceptsRejectedSubmission_RecordsCreditAndAdvances()
    {
        var session = SampleLiveSessions.EnrolledActive(SampleLiveSessions.TriviaStages(2));
        var submission = session.SubmitTriviaAnswer(HandlerScaffold.Team, SampleLiveSessions.WrongChoiceId, SampleLiveSessions.Now);
        var handler = Build(session, out var repository, out var notifier, out var scoring);

        var response = await handler.Handle(
            new OverrideValidationOutcomeCommand(submission.Id, true, "Equivalent answer accepted."), default);

        Assert.Equal(ValidationOutcome.Accepted.ToString(), response.NewOutcome);
        Assert.Equal(ValidationOutcome.Accepted, submission.Outcome);
        Assert.Equal(1, repository.SaveChangesCount);
        scoring.Verify(s => s.RecordStageCreditAsync(It.IsAny<RecordStageCreditRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        notifier.Verify(n => n.NotifyEvidenceSubmissionOutcomeChangedAsync(It.IsAny<EvidenceSubmissionOutcomeChangedPayload>(), It.IsAny<CancellationToken>()), Times.Once);
        notifier.Verify(n => n.NotifyTeamProgressChangedAsync(It.IsAny<TeamProgressChangedPayload>(), It.IsAny<CancellationToken>()), Times.Once);
        notifier.Verify(n => n.NotifySessionStateChangedAsync(It.IsAny<LiveSessionStateChangedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_KeepsRejected_DoesNotRecordCreditOrAdvance()
    {
        var session = SampleLiveSessions.EnrolledActive(SampleLiveSessions.TriviaStages(2));
        var submission = session.SubmitTriviaAnswer(HandlerScaffold.Team, SampleLiveSessions.WrongChoiceId, SampleLiveSessions.Now);
        var handler = Build(session, out _, out var notifier, out var scoring);

        var response = await handler.Handle(
            new OverrideValidationOutcomeCommand(submission.Id, false, "Still incorrect."), default);

        Assert.Equal(ValidationOutcome.Rejected.ToString(), response.NewOutcome);
        scoring.Verify(s => s.RecordStageCreditAsync(It.IsAny<RecordStageCreditRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        notifier.Verify(n => n.NotifyTeamProgressChangedAsync(It.IsAny<TeamProgressChangedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FinalizesSession_WhenLastStageAcceptedByOverride()
    {
        var session = SampleLiveSessions.EnrolledActive(SampleLiveSessions.TriviaStages(1));
        var submission = session.SubmitTriviaAnswer(HandlerScaffold.Team, SampleLiveSessions.WrongChoiceId, SampleLiveSessions.Now);
        var handler = Build(session, out _, out var notifier, out _);

        await handler.Handle(new OverrideValidationOutcomeCommand(submission.Id, true, "Operator confirmation."), default);

        Assert.Equal("Finalized", session.State.Name);
        notifier.Verify(n => n.NotifySessionStateChangedAsync(It.IsAny<LiveSessionStateChangedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static OverrideValidationOutcomeHandler Build(
        LiveSession session,
        out FakeLiveSessionRepository repository,
        out Mock<ISessionRealtimeNotifier> notifier,
        out Mock<IScoringMonitoringClient> scoring)
    {
        repository = new FakeLiveSessionRepository([session]);
        notifier = new Mock<ISessionRealtimeNotifier>();
        scoring = new Mock<IScoringMonitoringClient>();
        return new OverrideValidationOutcomeHandler(
            repository,
            HandlerScaffold.Clock(),
            HandlerScaffold.Operator("operator-1").Object,
            notifier.Object,
            scoring.Object);
    }
}

public sealed class SubmitTriviaAnswerHandlerTests
{
    [Fact]
    public async Task Handle_Throws_WhenSessionTeamNotFound()
    {
        var session = SampleLiveSessions.EnrolledActive(SampleLiveSessions.TriviaStages(2));
        var handler = Build(session, HandlerScaffold.Participant, out _, out _, out _);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(new SubmitTriviaAnswerCommand(Guid.NewGuid(), SampleLiveSessions.CorrectChoiceId), default));
        Assert.Equal("session_team_not_found", exception.Code);
    }

    [Fact]
    public async Task Handle_Throws_WhenParticipantNotOnTeam()
    {
        var session = SampleLiveSessions.EnrolledActive(SampleLiveSessions.TriviaStages(2));
        var handler = Build(session, "intruder-9", out _, out _, out _);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(new SubmitTriviaAnswerCommand(HandlerScaffold.Team, SampleLiveSessions.CorrectChoiceId), default));
        Assert.Equal("session_team_participation_required", exception.Code);
        Assert.Equal(UmbralFailureCategory.Forbidden, exception.Category);
    }

    [Fact]
    public async Task Handle_AcceptsCorrectAnswer_RecordsCreditAndPublishes()
    {
        var session = SampleLiveSessions.EnrolledActive(SampleLiveSessions.TriviaStages(2));
        var handler = Build(session, HandlerScaffold.Participant, out _, out var notifier, out var scoring);

        var response = await handler.Handle(
            new SubmitTriviaAnswerCommand(HandlerScaffold.Team, SampleLiveSessions.CorrectChoiceId), default);

        Assert.Equal(ValidationOutcome.Accepted.ToString(), response.ValidationOutcome);
        scoring.Verify(s => s.RecordStageCreditAsync(It.IsAny<RecordStageCreditRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        notifier.Verify(n => n.NotifyEvidenceSubmissionOutcomeChangedAsync(It.IsAny<EvidenceSubmissionOutcomeChangedPayload>(), It.IsAny<CancellationToken>()), Times.Once);
        notifier.Verify(n => n.NotifyTeamProgressChangedAsync(It.IsAny<TeamProgressChangedPayload>(), It.IsAny<CancellationToken>()), Times.Once);
        notifier.Verify(n => n.NotifySessionStateChangedAsync(It.IsAny<LiveSessionStateChangedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RejectsWrongAnswer_DoesNotRecordCredit()
    {
        var session = SampleLiveSessions.EnrolledActive(SampleLiveSessions.TriviaStages(2));
        var handler = Build(session, HandlerScaffold.Participant, out _, out var notifier, out var scoring);

        var response = await handler.Handle(
            new SubmitTriviaAnswerCommand(HandlerScaffold.Team, SampleLiveSessions.WrongChoiceId), default);

        Assert.Equal(ValidationOutcome.Rejected.ToString(), response.ValidationOutcome);
        scoring.Verify(s => s.RecordStageCreditAsync(It.IsAny<RecordStageCreditRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        notifier.Verify(n => n.NotifyEvidenceSubmissionOutcomeChangedAsync(It.IsAny<EvidenceSubmissionOutcomeChangedPayload>(), It.IsAny<CancellationToken>()), Times.Once);
        notifier.Verify(n => n.NotifyTeamProgressChangedAsync(It.IsAny<TeamProgressChangedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FinalizesSession_OnLastStageAcceptance()
    {
        var session = SampleLiveSessions.EnrolledActive(SampleLiveSessions.TriviaStages(1));
        var handler = Build(session, HandlerScaffold.Participant, out _, out var notifier, out _);

        await handler.Handle(new SubmitTriviaAnswerCommand(HandlerScaffold.Team, SampleLiveSessions.CorrectChoiceId), default);

        Assert.Equal("Finalized", session.State.Name);
        notifier.Verify(n => n.NotifySessionStateChangedAsync(It.IsAny<LiveSessionStateChangedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static SubmitTriviaAnswerHandler Build(
        LiveSession session,
        string participantUserId,
        out FakeLiveSessionRepository repository,
        out Mock<ISessionRealtimeNotifier> notifier,
        out Mock<IScoringMonitoringClient> scoring)
    {
        repository = new FakeLiveSessionRepository([session]);
        notifier = new Mock<ISessionRealtimeNotifier>();
        scoring = new Mock<IScoringMonitoringClient>();
        return new SubmitTriviaAnswerHandler(
            repository,
            HandlerScaffold.Clock(),
            HandlerScaffold.Participants(participantUserId).Object,
            notifier.Object,
            scoring.Object);
    }
}

public sealed class SubmitEvidenceCommandHandlerTests
{
    [Fact]
    public async Task Handle_Throws_WhenSessionTeamNotFound()
    {
        var session = SampleLiveSessions.EnrolledActive(SampleLiveSessions.TreasureStages(2));
        var handler = Build(session, HandlerScaffold.Participant, out _, out _, out _);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(new SubmitEvidenceCommand(Guid.NewGuid(), "qr-stage-1"), default));
        Assert.Equal("session_team_not_found", exception.Code);
    }

    [Fact]
    public async Task Handle_Throws_WhenParticipantNotOnTeam()
    {
        var session = SampleLiveSessions.EnrolledActive(SampleLiveSessions.TreasureStages(2));
        var handler = Build(session, "intruder-9", out _, out _, out _);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(new SubmitEvidenceCommand(HandlerScaffold.Team, "qr-stage-1"), default));
        Assert.Equal("session_team_participation_required", exception.Code);
    }

    [Fact]
    public async Task Handle_AcceptsMatchingHash_RecordsCreditAndAdvances()
    {
        var session = SampleLiveSessions.EnrolledActive(SampleLiveSessions.TreasureStages(2));
        var handler = Build(session, HandlerScaffold.Participant, out _, out var notifier, out var scoring);

        var response = await handler.Handle(new SubmitEvidenceCommand(HandlerScaffold.Team, "qr-stage-1"), default);

        Assert.Equal(ValidationOutcome.Accepted.ToString(), response.ValidationOutcome);
        scoring.Verify(s => s.RecordStageCreditAsync(It.IsAny<RecordStageCreditRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        notifier.Verify(n => n.NotifyTeamProgressChangedAsync(It.IsAny<TeamProgressChangedPayload>(), It.IsAny<CancellationToken>()), Times.Once);
        notifier.Verify(n => n.NotifySessionStateChangedAsync(It.IsAny<LiveSessionStateChangedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RejectsMismatchedHash_DoesNotRecordCredit()
    {
        var session = SampleLiveSessions.EnrolledActive(SampleLiveSessions.TreasureStages(2));
        var handler = Build(session, HandlerScaffold.Participant, out var repository, out var notifier, out var scoring);

        var response = await handler.Handle(new SubmitEvidenceCommand(HandlerScaffold.Team, "wrong-hash"), default);

        Assert.Equal(ValidationOutcome.Rejected.ToString(), response.ValidationOutcome);
        Assert.Equal(1, repository.SaveChangesCount);
        scoring.Verify(s => s.RecordStageCreditAsync(It.IsAny<RecordStageCreditRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        notifier.Verify(n => n.NotifyTeamProgressChangedAsync(It.IsAny<TeamProgressChangedPayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FinalizesSession_OnLastStageAcceptance()
    {
        var session = SampleLiveSessions.EnrolledActive(SampleLiveSessions.TreasureStages(1));
        var handler = Build(session, HandlerScaffold.Participant, out _, out var notifier, out _);

        await handler.Handle(new SubmitEvidenceCommand(HandlerScaffold.Team, "qr-stage-1"), default);

        Assert.Equal("Finalized", session.State.Name);
        notifier.Verify(n => n.NotifySessionStateChangedAsync(It.IsAny<LiveSessionStateChangedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static SubmitEvidenceCommandHandler Build(
        LiveSession session,
        string participantUserId,
        out FakeLiveSessionRepository repository,
        out Mock<ISessionRealtimeNotifier> notifier,
        out Mock<IScoringMonitoringClient> scoring)
    {
        repository = new FakeLiveSessionRepository([session]);
        notifier = new Mock<ISessionRealtimeNotifier>();
        scoring = new Mock<IScoringMonitoringClient>();
        return new SubmitEvidenceCommandHandler(
            repository,
            HandlerScaffold.Clock(),
            HandlerScaffold.Participants(participantUserId).Object,
            notifier.Object,
            scoring.Object);
    }
}

public sealed class ReleaseHintHandlerTests
{
    private static IReadOnlyList<LiveSessionStage> HintStages() =>
    [
        SampleLiveSessions.TreasureStage(1, hints: [SampleLiveSessions.Hint(1)]),
        SampleLiveSessions.TreasureStage(2)
    ];

    [Fact]
    public async Task Handle_Throws_WhenLiveSessionNotFound()
    {
        var session = SampleLiveSessions.EnrolledActive(HintStages());
        var handler = Build(session, out _, out _);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(new ReleaseHintCommand(Guid.NewGuid(), HandlerScaffold.Team, SampleLiveSessions.HintId(1)), default));
        Assert.Equal("live_session_not_found", exception.Code);
    }

    [Fact]
    public async Task Handle_ReleasesHintForSingleTeam()
    {
        var session = SampleLiveSessions.EnrolledActive(HintStages());
        var handler = Build(session, out var repository, out var notifier);

        var visibleHints = await handler.Handle(
            new ReleaseHintCommand(session.Id, HandlerScaffold.Team, SampleLiveSessions.HintId(1)), default);

        Assert.Single(visibleHints);
        Assert.Equal(SampleLiveSessions.HintId(1), visibleHints[0].HintId);
        Assert.Equal(1, repository.SaveChangesCount);
        var hintReleased = Assert.IsType<HintReleasedDomainEvent>(
            Assert.Single(session.DomainEvents, domainEvent => domainEvent is HintReleasedDomainEvent));
        Assert.Equal(session.Id, hintReleased.LiveSessionId);
        Assert.Equal(HandlerScaffold.Team, hintReleased.SessionTeamId);
        Assert.Equal(SampleLiveSessions.HintId(1), hintReleased.HintId);
        notifier.Verify(n => n.NotifyHintUnlockedAsync(It.IsAny<HintUnlockedPayload>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReleasesHintForEligibleTeams_WhenNoTeamSpecified()
    {
        var session = SampleLiveSessions.EnrolledActive(HintStages());
        var handler = Build(session, out _, out var notifier);

        var visibleHints = await handler.Handle(
            new ReleaseHintCommand(session.Id, null, SampleLiveSessions.HintId(1)), default);

        Assert.Single(visibleHints);
        notifier.Verify(n => n.NotifyHintUnlockedAsync(It.IsAny<HintUnlockedPayload>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Throws_WhenHintNotInFlowForEligibleRelease()
    {
        var session = SampleLiveSessions.EnrolledActive(HintStages());
        var handler = Build(session, out _, out _);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(new ReleaseHintCommand(session.Id, null, Guid.NewGuid()), default));
        Assert.Equal("released_hint_not_found", exception.Code);
    }

    [Fact]
    public async Task Handle_Throws_WhenNoEligibleTeamsRemain()
    {
        var session = SampleLiveSessions.EnrolledActive(HintStages());
        session.ReleaseHint(HandlerScaffold.Team, SampleLiveSessions.HintId(1), SampleLiveSessions.Now);
        var handler = Build(session, out _, out _);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(new ReleaseHintCommand(session.Id, null, SampleLiveSessions.HintId(1)), default));
        Assert.Equal("released_hint_no_eligible_session_teams", exception.Code);
    }

    private static ReleaseHintHandler Build(
        LiveSession session,
        out FakeLiveSessionRepository repository,
        out Mock<ISessionRealtimeNotifier> notifier)
    {
        repository = new FakeLiveSessionRepository([session]);
        notifier = new Mock<ISessionRealtimeNotifier>();
        return new ReleaseHintHandler(
            repository,
            HandlerScaffold.Clock(),
            notifier.Object);
    }
}
