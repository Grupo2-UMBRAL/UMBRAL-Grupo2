using Microsoft.AspNetCore.SignalR;

namespace ScoringAudit.Application.Hubs;

public sealed class ScoringAuditHub : Hub<IScoringAuditClient>
{
}
