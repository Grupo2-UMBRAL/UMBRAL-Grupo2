using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SessionManagement.Infrastructure.Realtime;

[Authorize]
public sealed class SessionManagementHub : Hub<ISessionClient>;
