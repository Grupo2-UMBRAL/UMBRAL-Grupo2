using Microsoft.AspNetCore.SignalR;
using Moq;
using SessionManagement.Infrastructure.Realtime;
using SessionManagement.Application.Features.SessionLifecycle;
using SessionManagement.Application.Abstractions.Realtime;
using SessionManagement.Application.Features.SessionSnapshots;
using Xunit;

namespace SessionManagement.IntegrationTests.Infrastructure;

public sealed class SignalRLiveSessionRealtimeNotifierTests
{
    private static readonly Guid LiveSessionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SessionTeamId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static (SignalRLiveSessionRealtimeNotifier Notifier, Mock<ISessionClient> Client) CreateNotifier()
    {
        var client = new Mock<ISessionClient>();
        var clients = new Mock<IHubClients<ISessionClient>>();
        clients.Setup(c => c.All).Returns(client.Object);
        var hub = new Mock<IHubContext<SessionManagementHub, ISessionClient>>();
        hub.Setup(h => h.Clients).Returns(clients.Object);

        return (new SignalRLiveSessionRealtimeNotifier(hub.Object), client);
    }

    [Fact]
    public async Task NotifySessionStateChangedAsync_BroadcastsPayloadWithMappedMetadata()
    {
        var (notifier, client) = CreateNotifier();
        client
            .Setup(c => c.ReceiveSessionStateChanged(It.IsAny<SessionStateChangedPayload>()))
            .Returns(Task.CompletedTask);
        var occurredAt = DateTimeOffset.UnixEpoch.AddMinutes(5);
        var stateChangedEvent = new LiveSessionStateChangedEvent(
            LiveSessionId,
            PreviousState: "Draft",
            State: "Open",
            RegisteredSessionTeamCount: 4,
            SequenceNumber: 7,
            Reason: "EnrollmentOpened",
            OccurredAtUtc: occurredAt);

        await notifier.NotifySessionStateChangedAsync(stateChangedEvent, CancellationToken.None);

        client.Verify(
            c => c.ReceiveSessionStateChanged(It.Is<SessionStateChangedPayload>(p =>
                p.PreviousState == "Draft" &&
                p.CurrentState == "Open" &&
                p.RemainingSeconds == null &&
                p.Metadata.LiveSessionId == LiveSessionId &&
                p.Metadata.SequenceNumber == 7 &&
                p.Metadata.OccurredAtUtc == occurredAt &&
                p.Metadata.RefreshPolicy == SnapshotRefreshPolicy.RefreshSnapshot &&
                p.Metadata.Reason == "EnrollmentOpened")),
            Times.Once);
    }

    [Fact]
    public async Task NotifyTeamProgressChangedAsync_BroadcastsSamePayload()
    {
        var (notifier, client) = CreateNotifier();
        client
            .Setup(c => c.ReceiveTeamProgressChanged(It.IsAny<TeamProgressChangedPayload>()))
            .Returns(Task.CompletedTask);
        var payload = new TeamProgressChangedPayload(
            BuildMetadata(),
            SessionTeamId,
            PreviousStage: null,
            CurrentStage: null,
            ProgressState: "InProgress");

        await notifier.NotifyTeamProgressChangedAsync(payload, CancellationToken.None);

        client.Verify(
            c => c.ReceiveTeamProgressChanged(It.Is<TeamProgressChangedPayload>(p =>
                ReferenceEquals(p, payload) &&
                p.Metadata.SequenceNumber == 11 &&
                p.SessionTeamId == SessionTeamId &&
                p.ProgressState == "InProgress")),
            Times.Once);
    }

    [Fact]
    public async Task NotifyEvidenceSubmissionOutcomeChangedAsync_BroadcastsSamePayload()
    {
        var (notifier, client) = CreateNotifier();
        client
            .Setup(c => c.ReceiveEvidenceSubmissionOutcomeChanged(It.IsAny<EvidenceSubmissionOutcomeChangedPayload>()))
            .Returns(Task.CompletedTask);
        var evidenceSubmissionId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var missionStageId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var payload = new EvidenceSubmissionOutcomeChangedPayload(
            BuildMetadata(),
            evidenceSubmissionId,
            SessionTeamId,
            missionStageId,
            CurrentOutcome: "Approved",
            PreviousOutcome: "PendingReview",
            FailureReason: null);

        await notifier.NotifyEvidenceSubmissionOutcomeChangedAsync(payload, CancellationToken.None);

        client.Verify(
            c => c.ReceiveEvidenceSubmissionOutcomeChanged(It.Is<EvidenceSubmissionOutcomeChangedPayload>(p =>
                ReferenceEquals(p, payload) &&
                p.EvidenceSubmissionId == evidenceSubmissionId &&
                p.CurrentOutcome == "Approved" &&
                p.Metadata.SequenceNumber == 11)),
            Times.Once);
    }

    [Fact]
    public async Task NotifyHintUnlockedAsync_BroadcastsSamePayload()
    {
        var (notifier, client) = CreateNotifier();
        client
            .Setup(c => c.ReceiveHintUnlocked(It.IsAny<HintUnlockedPayload>()))
            .Returns(Task.CompletedTask);
        var hintId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var missionStageId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var hint = new VisibleHintSnapshot(
            hintId,
            missionStageId,
            Content: "Look north.",
            IsSolution: false,
            Latitude: null,
            Longitude: null,
            UnlockedAtUtc: DateTimeOffset.UnixEpoch,
            UnlockReason: "OperatorReleased");
        var payload = new HintUnlockedPayload(BuildMetadata(), SessionTeamId, hint);

        await notifier.NotifyHintUnlockedAsync(payload, CancellationToken.None);

        client.Verify(
            c => c.ReceiveHintUnlocked(It.Is<HintUnlockedPayload>(p =>
                ReferenceEquals(p, payload) &&
                p.Hint.HintId == hintId &&
                p.SessionTeamId == SessionTeamId &&
                p.Metadata.SequenceNumber == 11)),
            Times.Once);
    }

    private static RealtimeEventMetadata BuildMetadata() => new(
        LiveSessionId,
        SequenceNumber: 11,
        OccurredAtUtc: DateTimeOffset.UnixEpoch,
        RefreshPolicy: SnapshotRefreshPolicy.ApplyIncremental,
        Reason: "Update");
}
