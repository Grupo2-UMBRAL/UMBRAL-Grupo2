using Microsoft.AspNetCore.SignalR;

namespace ScoringMonitoring.Api.Hubs;

public sealed class ScoringMonitoringHub : Hub<IScoringMonitoringClient>
{
}
