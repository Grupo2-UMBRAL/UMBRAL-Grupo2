using MediatR;
using Microsoft.AspNetCore.SignalR;
using Umbral.ScoringAudit.Api.Domain.Audit;
using Umbral.ScoringAudit.Api.Hubs;
using Umbral.ScoringAudit.Api.Hubs.Contracts;
using Umbral.ScoringAudit.Api.Infrastructure;
using Umbral.ServiceDefaults;

namespace Umbral.ScoringAudit.Api.Application.Audit;

public sealed record LogSessionEventCommand(
    Guid LiveSessionId,
    string EventType,
    string Description) : IRequest<SessionEventLogPayload>, IAuthorizableRequest
{
    public RequestAuthorizationMetadata Authorization => UmbralRequestAuthorizations.AdministratorOrOperator;
}

public sealed class LogSessionEventHandler(
    ScoringAuditDbContext dbContext,
    TimeProvider timeProvider,
    IHubContext<ScoringAuditHub, IScoringAuditClient> hubContext)
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

        dbContext.SessionEventLogs.Add(eventLog);
        await dbContext.SaveChangesAsync(cancellationToken);

        var payload = SessionEventLogPayload.FromEntity(eventLog);
        await hubContext.Clients.All.ReceiveEventLogUpdated(payload).WaitAsync(cancellationToken);

        return payload;
    }
}
