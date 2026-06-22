using ScoringMonitoring.Application.Abstractions;
using ScoringMonitoring.Domain.Audit;

namespace ScoringMonitoring.Application.Features.SessionEventLogs.Commands.LogSessionEvent;

public sealed class LogSessionEventHandler(
    IUnitOfWork unitOfWork, IRepository<SessionEventLog> sessionEventLogRepository,
    TimeProvider timeProvider,
    IScoringMonitoringUpdatesPublisher updatesPublisher)
    : IRequestHandler<LogSessionEventCommand, SessionEventLogPayload>
{
    public async Task<SessionEventLogPayload> Handle(
        LogSessionEventCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var eventLog = new SessionEventLog(
            Guid.NewGuid(),
            request.LiveSessionId,
            request.EventType,
            request.Description,
            timeProvider.GetUtcNow());

        sessionEventLogRepository.Add(eventLog);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var payload = SessionEventLogPayload.FromEntity(eventLog);
        await updatesPublisher.PublishEventLogUpdatedAsync(payload, cancellationToken);

        return payload;
    }
}


