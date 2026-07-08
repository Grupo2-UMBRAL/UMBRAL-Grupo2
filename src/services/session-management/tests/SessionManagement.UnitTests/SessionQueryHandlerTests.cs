using Moq;
using Umbral.ServiceDefaults;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Application.Abstractions;
using SessionManagement.Application.Features.SessionEnrollment;
using SessionManagement.Application.Features.SessionSnapshots;
using Xunit;

namespace SessionManagement.UnitTests;

// Tier 1 query-handler tests. These handlers are mostly projection/mapping
// branches: drive them with sessions that exercise present/absent/empty
// collections, unknown stage references, and state-dependent visibility.
file static class QueryScaffold
{
    public const string Participant = "participant-1";

    public static readonly Guid Team = SampleLiveSessions.TeamId(1);

    public static IRepository<LiveSession> Repository(LiveSession session) => new FakeRepository<LiveSession>([session]);

    public static IUnitOfWork UnitOfWork() => new Mock<IUnitOfWork>().Object;

    public static TimeProvider Clock() => new FixedTimeProvider(SampleLiveSessions.Now);

    public static ICurrentParticipantIdentity Participants(string participantUserId)
    {
        var identity = new Mock<ICurrentParticipantIdentity>();
        identity.Setup(i => i.GetRequiredParticipantUserId()).Returns(ParticipantUserId.Parse(participantUserId));
        return identity.Object;
    }
}

public sealed class GetLiveSessionOverviewQueryHandlerTests
{
    private static GetLiveSessionOverviewQueryHandler Build(LiveSession session)
        => new(QueryScaffold.Repository(session), QueryScaffold.Clock());

    [Fact]
    public async Task Handle_Throws_WhenLiveSessionNotFound()
    {
        var handler = Build(SampleLiveSessions.EnrolledActive());

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(new GetLiveSessionOverviewQuery(Guid.NewGuid()), default));
        Assert.Equal("live_session_not_found", exception.Code);
    }

    [Fact]
    public async Task Handle_ProjectsTeamsAndReleasedHints_ForActiveSession()
    {
        var session = SampleLiveSessions.EnrolledActive(
        [
            SampleLiveSessions.TreasureStage(1, hints: [SampleLiveSessions.Hint(1)]),
            SampleLiveSessions.TreasureStage(2)
        ]);
        session.ReleaseHint(QueryScaffold.Team, SampleLiveSessions.HintId(1), SampleLiveSessions.Now);
        var handler = Build(session);

        var overview = await handler.Handle(new GetLiveSessionOverviewQuery(session.Id), default);

        var team = Assert.Single(overview.SessionTeams);
        Assert.Equal(1, team.ParticipantCount);
        Assert.Single(team.ReleasedHints);
        Assert.Equal("Stage 1", team.CurrentStage?.Name);
        Assert.Null(overview.RemainingSeconds);
    }

    [Fact]
    public async Task Handle_ReturnsPositiveRemainingSeconds_ForScheduledSessionWithFutureStart()
    {
        var session = SampleLiveSessions.ScheduledWithStart(SampleLiveSessions.Now.AddMinutes(10));
        var handler = Build(session);

        var overview = await handler.Handle(new GetLiveSessionOverviewQuery(session.Id), default);

        Assert.Equal(600, overview.RemainingSeconds);
    }

    [Fact]
    public async Task Handle_ReturnsZeroRemainingSeconds_WhenScheduledStartHasPassed()
    {
        var session = SampleLiveSessions.ScheduledWithStart(SampleLiveSessions.Now.AddMinutes(-5));
        var handler = Build(session);

        var overview = await handler.Handle(new GetLiveSessionOverviewQuery(session.Id), default);

        Assert.Equal(0, overview.RemainingSeconds);
    }

    [Fact]
    public async Task Handle_ReturnsNullRemainingSeconds_WhenScheduledStartIsUnset()
    {
        var session = SampleLiveSessions.ScheduledWithStart(scheduledStartAtUtc: null);
        var handler = Build(session);

        var overview = await handler.Handle(new GetLiveSessionOverviewQuery(session.Id), default);

        Assert.Null(overview.RemainingSeconds);
    }
}

public sealed class GetSessionTeamDetailQueryHandlerTests
{
    private static GetSessionTeamDetailQueryHandler Build(LiveSession session)
        => new(QueryScaffold.Repository(session), QueryScaffold.Clock());

    [Fact]
    public async Task Handle_Throws_WhenSessionTeamNotFound()
    {
        var session = SampleLiveSessions.EnrolledActive();
        var handler = Build(session);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(new GetSessionTeamDetailQuery(session.Id, Guid.NewGuid()), default));
        Assert.Equal("session_team_not_found", exception.Code);
    }

    [Fact]
    public async Task Handle_FlagsInactive_WhenNoRecentActivity()
    {
        var session = SampleLiveSessions.EnrolledActive(SampleLiveSessions.TreasureStages(2));
        var handler = Build(session);

        var detail = await handler.Handle(new GetSessionTeamDetailQuery(session.Id, QueryScaffold.Team), default);

        Assert.Empty(detail.EvidenceSubmissions);
        Assert.Empty(detail.ReleasedHints);
        Assert.True(detail.IsInactive);
    }

    [Fact]
    public async Task Handle_NormalizesNonPositiveInactivityThreshold_ToOne()
    {
        var session = SampleLiveSessions.EnrolledActive(SampleLiveSessions.TreasureStages(2));
        var handler = Build(session);

        var detail = await handler.Handle(
            new GetSessionTeamDetailQuery(session.Id, QueryScaffold.Team, InactivityThresholdMinutes: -5), default);

        Assert.Equal(1, detail.InactivityThresholdMinutes);
    }

    [Fact]
    public async Task Handle_MapsRejectedTriviaSubmission_AsCorrectionEligible()
    {
        var session = SampleLiveSessions.EnrolledActive(SampleLiveSessions.TriviaStages(2));
        session.SubmitTriviaAnswer(QueryScaffold.Team, SampleLiveSessions.WrongChoiceId, SampleLiveSessions.Now);
        var handler = Build(session);

        var detail = await handler.Handle(new GetSessionTeamDetailQuery(session.Id, QueryScaffold.Team), default);

        var submission = Assert.Single(detail.EvidenceSubmissions);
        Assert.Equal(ValidationOutcome.Rejected.ToString(), submission.ValidationOutcome);
        Assert.True(submission.IsTriviaCorrectionEligible);
        Assert.False(detail.IsInactive);
    }

    [Fact]
    public async Task Handle_MapsReleasedHint_WhenStageAndHintResolve()
    {
        var session = SampleLiveSessions.EnrolledActive(
        [
            SampleLiveSessions.TreasureStage(1, hints: [SampleLiveSessions.Hint(1)]),
            SampleLiveSessions.TreasureStage(2)
        ]);
        session.ReleaseHint(QueryScaffold.Team, SampleLiveSessions.HintId(1), SampleLiveSessions.Now);
        var handler = Build(session);

        var detail = await handler.Handle(new GetSessionTeamDetailQuery(session.Id, QueryScaffold.Team), default);

        var hint = Assert.Single(detail.ReleasedHints);
        Assert.Equal("Stage 1", hint.StageName);
    }

    [Fact]
    public async Task Handle_FallsBackOnUnknownStage_AndSkipsUnresolvableHint()
    {
        var session = SampleLiveSessions.EnrolledActive(SampleLiveSessions.TreasureStages(2));
        var ghostStage = LiveSessionStage.Create(Guid.NewGuid(), "Ghost", 1, 1, 10, "Hard", "Trivia", "Prompt");
        session.EvidenceSubmissions.Add(EvidenceSubmission.CreateTrivia(
            session.Id, QueryScaffold.Team, ghostStage, SampleLiveSessions.WrongChoiceId,
            ValidationOutcome.Rejected, "trivia_answer_mismatch", SampleLiveSessions.Now));
        session.ReleasedHints.Add(ReleasedHint.Create(
            session.Id, QueryScaffold.Team, Guid.NewGuid(), Guid.NewGuid(), SampleLiveSessions.Now, "Manual"));
        var handler = Build(session);

        var detail = await handler.Handle(new GetSessionTeamDetailQuery(session.Id, QueryScaffold.Team), default);

        var submission = Assert.Single(detail.EvidenceSubmissions);
        Assert.Equal("Unknown Session Stage", submission.StageName);
        Assert.Equal("Unknown", submission.Difficulty);
        Assert.Empty(detail.ReleasedHints);
    }
}

public sealed class GetSessionTeamSnapshotQueryHandlerTests
{
    private static GetSessionTeamSnapshotQueryHandler Build(LiveSession session, string participantUserId = QueryScaffold.Participant)
        => new(
            QueryScaffold.Repository(session),
            QueryScaffold.Participants(participantUserId),
            QueryScaffold.Clock());

    [Fact]
    public async Task Handle_Throws_WhenSessionTeamNotFound()
    {
        var handler = Build(SampleLiveSessions.EnrolledActive());

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(new GetSessionTeamSnapshotQuery(Guid.NewGuid()), default));
        Assert.Equal("session_team_not_found", exception.Code);
    }

    [Fact]
    public async Task Handle_Throws_WhenParticipantNotOnTeam()
    {
        var session = SampleLiveSessions.EnrolledActive();
        var handler = Build(session, "intruder-9");

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(new GetSessionTeamSnapshotQuery(QueryScaffold.Team), default));
        Assert.Equal("session_team_participation_required", exception.Code);
        Assert.Equal(UmbralFailureCategory.Forbidden, exception.Category);
    }

    [Fact]
    public async Task Handle_ReturnsActiveSnapshot_WithoutAllStages_AndHidesSolutionHint()
    {
        var session = SampleLiveSessions.EnrolledActive(
        [
            SampleLiveSessions.TreasureStage(1, hints: [SampleLiveSessions.SolutionHint(1)]),
            SampleLiveSessions.TreasureStage(2)
        ]);
        session.ReleaseHint(QueryScaffold.Team, SampleLiveSessions.HintId(1), SampleLiveSessions.Now);
        var handler = Build(session);

        var snapshot = await handler.Handle(new GetSessionTeamSnapshotQuery(QueryScaffold.Team), default);

        Assert.Null(snapshot.AllStages);
        Assert.Empty(snapshot.VisibleHints);
        Assert.Equal("Stage 1", snapshot.CurrentStage?.Name);
    }

    [Fact]
    public async Task Handle_RevealsAllStagesAndSolutionHint_WhenFinalized()
    {
        var session = SampleLiveSessions.EnrolledActive(
        [
            SampleLiveSessions.TreasureStage(1, hints: [SampleLiveSessions.SolutionHint(1)]),
            SampleLiveSessions.TreasureStage(2)
        ]);
        session.ReleaseHint(QueryScaffold.Team, SampleLiveSessions.HintId(1), SampleLiveSessions.Now);
        SampleLiveSessions.ForceState(session, LiveSessionStates.Finalized);
        var handler = Build(session);

        var snapshot = await handler.Handle(new GetSessionTeamSnapshotQuery(QueryScaffold.Team), default);

        Assert.NotNull(snapshot.AllStages);
        Assert.Equal(2, snapshot.AllStages!.Count);
        Assert.Single(snapshot.VisibleHints);
    }
}
