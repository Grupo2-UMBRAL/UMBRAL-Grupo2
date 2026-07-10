using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ScoringMonitoring.Application.Features.SessionEventLogs.Commands.LogSessionEvent;

namespace ScoringMonitoring.Infrastructure.Messaging;

/// <summary>
/// Background consumer that subscribes to session-management's audit events and persists
/// each one by dispatching the existing <see cref="LogSessionEventCommand"/> (which persists
/// the SessionEventLog and pushes the realtime update over SignalR). Delivery is at-least-once,
/// so downstream handling must tolerate the same event arriving more than once.
/// </summary>
public sealed class SessionAuditEventConsumer(
    RabbitMqConnection connection,
    IServiceScopeFactory scopeFactory,
    ILogger<SessionAuditEventConsumer> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "RabbitMQ consumer could not start; retrying in 5s.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var rabbit = await connection.GetConnectionAsync(stoppingToken);
        var channel = await rabbit.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(
            exchange: UmbralAuditMessaging.Exchange,
            type: ExchangeType.Topic,
            durable: true,
            cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(
            queue: UmbralAuditMessaging.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);
        await channel.QueueBindAsync(
            queue: UmbralAuditMessaging.Queue,
            exchange: UmbralAuditMessaging.Exchange,
            routingKey: UmbralAuditMessaging.RoutingKey,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, eventArgs) => HandleMessageAsync(channel, eventArgs, stoppingToken);

        await channel.BasicConsumeAsync(
            queue: UmbralAuditMessaging.Queue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation(
            "Listening for session audit events on queue {Queue}.",
            UmbralAuditMessaging.Queue);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleMessageAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        CancellationToken stoppingToken)
    {
        try
        {
            var message = JsonSerializer.Deserialize<SessionAuditEventMessage>(eventArgs.Body.Span);
            if (message is null)
            {
                logger.LogWarning("Received an empty audit event; acking and discarding.");
                await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, stoppingToken);
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(
                new LogSessionEventCommand(
                    message.LiveSessionId,
                    message.EventType,
                    message.Description,
                    message.EventId),
                stoppingToken);

            await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, stoppingToken);
            logger.LogInformation(
                "Persisted audit event {EventType} for LiveSession {LiveSessionId}.",
                message.EventType,
                message.LiveSessionId);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to process audit event; requeueing.");
            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true, stoppingToken);
        }
    }
}
