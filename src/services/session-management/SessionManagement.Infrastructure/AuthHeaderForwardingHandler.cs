using System.Net.Http.Headers;

namespace SessionManagement.Infrastructure;

public sealed class AuthHeaderForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authorizationHeader = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorizationHeader) && request.Headers.Authorization is null)
        {
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorizationHeader);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
