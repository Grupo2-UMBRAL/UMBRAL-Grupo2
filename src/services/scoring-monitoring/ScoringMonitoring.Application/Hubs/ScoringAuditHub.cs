using Microsoft.AspNetCore.SignalR;

namespace ScoringMonitoring.Application.Hubs;

public sealed class ScoringMonitoringHub : Hub<IScoringMonitoringClient>
{
}
