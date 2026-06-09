using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;

namespace Umbral.ScoringAudit.Api.Application.Audit;

public sealed record GetSessionEventLogQuery(Guid LiveSessionId)
    : IRequest<IReadOnlyList<SessionEventLogPayload>>, IAuthorizableRequest
{
    public RequestAuthorizationMetadata Authorization => UmbralRequestAuthorizations.AdministratorOrOperator;
}

public sealed class GetSessionEventLogHandler(IScoringAuditDbContext dbContext)
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

        return await dbContext.SessionEventLogs
            .AsNoTracking()
            .Where(eventLog => eventLog.LiveSessionId == request.LiveSessionId)
            .OrderByDescending(eventLog => eventLog.Timestamp)
            .ThenByDescending(eventLog => eventLog.Id)
            .Select(eventLog => new SessionEventLogPayload(
                eventLog.Id,
                eventLog.LiveSessionId,
                eventLog.EventType,
                eventLog.Description,
                eventLog.Timestamp))
            .ToListAsync(cancellationToken);
    }
}
