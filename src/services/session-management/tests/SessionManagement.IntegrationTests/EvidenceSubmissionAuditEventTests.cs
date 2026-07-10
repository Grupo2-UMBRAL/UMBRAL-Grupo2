using System.Reflection;
using SessionManagement.Domain.LiveSessions;
using Xunit;

namespace SessionManagement.IntegrationTests;

/// <summary>
/// Audit is emitted as domain events raised by the <see cref="LiveSession"/> aggregate and
/// published to RabbitMQ through MassTransit's transactional outbox during SaveChanges. These
/// tests assert on the raised domain events (a stronger contract than mocking a publisher),
/// covering the submitted and immediate-validation facts for both accepted and rejected evidence.
/// </summary>
public sealed class EvidenceSubmissionAuditEventTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 6, 4, 5, 0, 0, TimeSpan.Zero);
    private static readonly Guid LiveSessionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid MissionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid StageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TeamId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void SubmitEvidence_RaisesSubmittedAndValidatedDomainEvents_WhenRejected()
    {
        var liveSession = CreateActiveLiveSession();

        var submission = liveSession.SubmitEvidence(TeamId, "wrong-qr", NowUtc);

        Assert.Equal(ValidationOutcome.Rejected, submission.Outcome);
        Assert.Collection(
            liveSession.DomainEvents,
            domainEvent =>
            {
                var submitted = Assert.IsType<EvidenceSubmittedDomainEvent>(domainEvent);
                Assert.Equal(LiveSessionId, submitted.LiveSessionId);
                Assert.Equal(submission.Id, submitted.EvidenceSubmissionId);
                Assert.Equal(TeamId, submitted.SessionTeamId);
                Assert.Equal(StageId, submitted.MissionStageId);
                Assert.Equal("TreasureHunt", submitted.GameType);
                Assert.NotEqual(Guid.Empty, submitted.EventId);
                Assert.Equal(NowUtc, submitted.OccurredOnUtc);
            },
            domainEvent =>
            {
                var validated = Assert.IsType<EvidenceValidatedDomainEvent>(domainEvent);
                Assert.Equal(LiveSessionId, validated.LiveSessionId);
                Assert.Equal(submission.Id, validated.EvidenceSubmissionId);
                Assert.Equal(TeamId, validated.SessionTeamId);
                Assert.Equal(StageId, validated.MissionStageId);
                Assert.Equal("Rejected", validated.Outcome);
                Assert.Equal("AutomaticTreasureHunt", validated.Source);
            });
    }

    [Fact]
    public void SubmitEvidence_RaisesValidatedEventWithAcceptedOutcome_WhenHashMatches()
    {
        var liveSession = CreateActiveLiveSession();

        var submission = liveSession.SubmitEvidence(TeamId, "expected-qr", NowUtc);

        Assert.Equal(ValidationOutcome.Accepted, submission.Outcome);
        var validated = Assert.IsType<EvidenceValidatedDomainEvent>(
            Assert.Single(liveSession.DomainEvents, domainEvent => domainEvent is EvidenceValidatedDomainEvent));
        Assert.Equal("Accepted", validated.Outcome);
        Assert.Equal("AutomaticTreasureHunt", validated.Source);
    }

    [Fact]
    public void EachRaisedDomainEvent_HasItsOwnEventId()
    {
        var liveSession = CreateActiveLiveSession();

        liveSession.SubmitEvidence(TeamId, "wrong-qr", NowUtc);

        var eventIds = liveSession.DomainEvents.Select(domainEvent => domainEvent.EventId).ToArray();
        Assert.Equal(eventIds.Length, eventIds.Distinct().Count());
    }

    private static LiveSession CreateActiveLiveSession()
    {
        var liveSession = LiveSession.Create(
            LiveSessionId,
            MissionId,
            "Evidence Mission",
            "Wave A",
            scheduledStartAtUtc: null,
            createdAtUtc: NowUtc.AddMinutes(-30),
            sessionStageFlow:
            [
                LiveSessionStage.Create(
                    StageId,
                    "QR Stage",
                    1,
                    1,
                    15,
                    "Easy",
                    "TreasureHunt",
                    "Find the seal.",
                    expectedQrHash: "expected-qr")
            ]);

        liveSession.AssignJoinCode(JoinCode.Parse("ABC234"));
        liveSession.OpenEnrollmentWindow(NowUtc.AddMinutes(-20));
        var team = liveSession.RegisterTeam(TeamId, "Alpha Team", JoinCode.Parse("ABC234"), NowUtc.AddMinutes(-10));
        liveSession.EnrollParticipantInTeam(team.Id, "participant-alpha", JoinCode.Parse("ABC234"), NowUtc.AddMinutes(-10));
        ForceState(liveSession, LiveSessionStates.Active);
        return liveSession;
    }

    private static void ForceState(LiveSession liveSession, string state)
    {
        var backingField = typeof(LiveSession).GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("LiveSession.State backing field was not found.");

        backingField.SetValue(liveSession, state);
    }
}
