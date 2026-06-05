using Microsoft.AspNetCore.SignalR;
using Umbral.ScoringAudit.Api.Hubs.Contracts;

namespace Umbral.ScoringAudit.Api.Hubs;

public sealed class ScoringAuditHub : Hub<IScoringAuditClient>
{
}
