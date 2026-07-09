using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SessionManagement.Api.Realtime;

[Authorize]
public sealed class SessionManagementHub : Hub<ISessionClient>;
