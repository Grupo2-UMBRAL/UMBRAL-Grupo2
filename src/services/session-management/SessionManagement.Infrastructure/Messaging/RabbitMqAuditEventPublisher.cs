using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace SessionManagement.Infrastructure.Messaging;

/// <summary>
/// Publishes an already-built audit message to the RabbitMQ topic exchange. Called by the
/// domain-events dispatch interceptor <b>after</b> the business transaction commits, in
/// best-effort mode: if the broker is unreachable the event is logged and dropped
/// (see ADR-013 — audit domain events without a transactional outbox).
/// </summary>
public sealed class RabbitMqAuditEventPublisher(
    RabbitMqConnection connection,
    ILogger<RabbitMqAuditEventPublisher> logger)
{
    public async Task PublishAsync(SessionAuditEventMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var rabbit = await connection.GetConnectionAsync(cancellationToken);
            await using var channel = await rabbit.CreateChannelAsync(cancellationToken: cancellationToken);

            await channel.ExchangeDeclareAsync(
                exchange: UmbralAuditMessaging.Exchange,
                type: ExchangeType.Topic,
                durable: true,
                cancellationToken: cancellationToken);

            var body = JsonSerializer.SerializeToUtf8Bytes(message);

            await channel.BasicPublishAsync(
                exchange: UmbralAuditMessaging.Exchange,
                routingKey: UmbralAuditMessaging.RoutingKey,
                body: body,
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "Published audit event {EventType} ({EventId}) for LiveSession {LiveSessionId}.",
                message.EventType,
                message.EventId,
                message.LiveSessionId);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to publish audit event {EventType} ({EventId}) for LiveSession {LiveSessionId} to RabbitMQ.",
                message.EventType,
                message.EventId,
                message.LiveSessionId);
        }
    }
}
