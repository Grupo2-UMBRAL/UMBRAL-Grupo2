using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Umbral.ServiceDefaults;

public sealed class HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public CurrentUser GetCurrentUser()
    {
        var principal = httpContextAccessor.HttpContext?.User
            ?? new ClaimsPrincipal(new ClaimsIdentity());

        return new CurrentUser(principal);
    }
}
