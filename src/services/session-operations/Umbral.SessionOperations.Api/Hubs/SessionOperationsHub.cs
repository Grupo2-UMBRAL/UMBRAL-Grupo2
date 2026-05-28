using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Umbral.SessionOperations.Api.Hubs;

[Authorize]
public sealed class SessionOperationsHub : Hub;
