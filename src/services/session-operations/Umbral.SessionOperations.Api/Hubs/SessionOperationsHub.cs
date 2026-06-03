using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Umbral.SessionOperations.Api.Hubs.Contracts;

namespace Umbral.SessionOperations.Api.Hubs;

[Authorize]
public sealed class SessionOperationsHub : Hub<ISessionClient>;
