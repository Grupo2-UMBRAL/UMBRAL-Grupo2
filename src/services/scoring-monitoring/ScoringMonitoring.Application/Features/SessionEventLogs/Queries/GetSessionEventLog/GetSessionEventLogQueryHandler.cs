using ScoringMonitoring.Domain.Audit;
using ScoringMonitoring.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ScoringMonitoring.Application.Features.SessionEventLogs.Queries.GetSessionEventLog;

public sealed class GetSessionEventLogHandler(ISessionEventLogRepository sessionEventLogRepository)
    : IRequestHandler<GetSessionEventLogQuery, IReadOnlyList<SessionEventLogPayload>>
{
    public async Task<IReadOnlyList<SessionEventLogPayload>> Handle(
        GetSessionEventLogQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.LiveSessionId == Guid.Empty)
        {
            throw new UmbralDomainException(
                "session_event_log.empty_live_session_id",
                "LiveSession id is required.",
                UmbralFailureCategory.Validation);
        }

        var eventLogs = await sessionEventLogRepository.GetByLiveSessionIdAsync(request.LiveSessionId, cancellationToken);
        
        return eventLogs
            .OrderByDescending(eventLog => eventLog.Timestamp)
            .ThenByDescending(eventLog => eventLog.Id)
            .Select(eventLog => new SessionEventLogPayload(
                eventLog.Id,
                eventLog.LiveSessionId,
                eventLog.EventType,
                eventLog.Description,
                eventLog.Timestamp))
            .ToList();
    }
}




