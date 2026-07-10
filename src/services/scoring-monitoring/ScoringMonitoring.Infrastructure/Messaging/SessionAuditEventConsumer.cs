using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoringMonitoring.Application.Features.SessionEventLogs.Commands.LogSessionEvent;
using Umbral.Contracts.Audit;

namespace ScoringMonitoring.Infrastructure.Messaging;

/// <summary>
/// Consumes session-management's audit events and persists each one by dispatching the existing
/// <see cref="LogSessionEventCommand"/> (which stores the SessionEventLog and pushes the realtime
/// update over SignalR). MassTransit owns the transport concerns the old hand-rolled
/// BackgroundService did manually — subscription, ack/nack, and retry.
/// <para>
/// Delivery is at-least-once, so the same event can arrive more than once. Deduplication is by
/// <see cref="SessionAuditEventMessage.EventId"/>: the command carries it as the log's identity and
/// <see cref="LogSessionEventHandler"/> short-circuits on a duplicate. That is why no MassTransit
/// inbox is configured here — idempotency already lives in the handler.
/// </para>
/// </summary>
public sealed class SessionAuditEventConsumer(ISender sender, ILogger<SessionAuditEventConsumer> logger)
    : IConsumer<SessionAuditEventMessage>
{
    public async Task Consume(ConsumeContext<SessionAuditEventMessage> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = context.Message;

        await sender.Send(
            new LogSessionEventCommand(
                message.LiveSessionId,
                message.EventType,
                message.Description,
                message.EventId),
            context.CancellationToken);

        logger.LogInformation(
            "Persisted audit event {EventType} ({EventId}) for LiveSession {LiveSessionId}.",
            message.EventType,
            message.EventId,
            message.LiveSessionId);
    }
}
