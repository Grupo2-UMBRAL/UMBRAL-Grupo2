using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SessionOperations.Application.Hubs.Contracts;

namespace SessionOperations.Application.Hubs;

[Authorize]
public sealed class SessionOperationsHub : Hub<ISessionClient>;
