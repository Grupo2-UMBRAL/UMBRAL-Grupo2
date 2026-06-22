using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SessionManagement.Application.Hubs.Contracts;

namespace SessionManagement.Api.Hubs;

[Authorize]
public sealed class SessionManagementHub : Hub<ISessionClient>;
