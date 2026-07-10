using SessionManagement.Domain.LiveSessions;
using SessionManagement.Infrastructure.Messaging;
using Xunit;

namespace SessionManagement.IntegrationTests;

/// <summary>
/// The audit description templates live in Infrastructure (not the domain event). These tests
/// lock the wire shape (event type + rendered description) produced for each domain event, plus
/// the identity/timestamp carried through from the event.
/// </summary>
public sealed class SessionAuditEventMapperTests
{
    private static readonly DateTimeOffset OccurredOnUtc = new(2026, 6, 4, 5, 0, 0, TimeSpan.Zero);
    private static readonly Guid LiveSessionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SubmissionId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid TeamId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid StageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid HintId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public void Map_EvidenceSubmitted_ProducesSubmittedAuditMessage()
    {
        var domainEvent = new EvidenceSubmittedDomainEvent(
            LiveSessionId, SubmissionId, TeamId, StageId, "TreasureHunt", OccurredOnUtc);

        var message = SessionAuditEventMapper.Map(domainEvent);

        Assert.NotNull(message);
        Assert.Equal(domainEvent.EventId, message!.EventId);
        Assert.Equal(LiveSessionId, message.LiveSessionId);
        Assert.Equal("EvidenceSubmitted", message.EventType);
        Assert.Equal(
            $"Session Team '{TeamId}' submitted evidence for Mission Stage '{StageId}'. Game Type: TreasureHunt.",
            message.Description);
        Assert.Equal(OccurredOnUtc, message.OccurredAtUtc);
    }

    [Fact]
    public void Map_EvidenceValidated_ProducesValidationOutcomeAuditMessage()
    {
        var domainEvent = new EvidenceValidatedDomainEvent(
            LiveSessionId, SubmissionId, TeamId, StageId, "Accepted", "AutomaticTrivia", OccurredOnUtc);

        var message = SessionAuditEventMapper.Map(domainEvent);

        Assert.NotNull(message);
        Assert.Equal("ValidationOutcome", message!.EventType);
        Assert.Equal(
            $"Evidence submission '{SubmissionId}' for Session Team '{TeamId}' on Mission Stage '{StageId}' was validated as Accepted. Source: AutomaticTrivia.",
            message.Description);
    }

    [Fact]
    public void Map_ValidationOutcomeOverridden_ProducesOperatorOverrideAuditMessage()
    {
        var domainEvent = new ValidationOutcomeOverriddenDomainEvent(
            LiveSessionId, SubmissionId, TeamId, StageId, "Accepted", "Manual confirmation", OccurredOnUtc);

        var message = SessionAuditEventMapper.Map(domainEvent);

        Assert.NotNull(message);
        Assert.Equal("ValidationOutcome", message!.EventType);
        Assert.Equal(
            $"Evidence submission '{SubmissionId}' for Session Team '{TeamId}' on Mission Stage '{StageId}' was validated as Accepted. Source: OperatorOverride. Reason: Manual confirmation.",
            message.Description);
    }

    [Fact]
    public void Map_HintReleased_ProducesHintReleasedAuditMessage()
    {
        var domainEvent = new HintReleasedDomainEvent(
            LiveSessionId, TeamId, StageId, HintId, "Manual", OccurredOnUtc);

        var message = SessionAuditEventMapper.Map(domainEvent);

        Assert.NotNull(message);
        Assert.Equal("HintReleased", message!.EventType);
        Assert.Equal(
            $"Hint '{HintId}' released to Session Team '{TeamId}' for Mission Stage '{StageId}'. Reason: Manual.",
            message.Description);
    }
}
