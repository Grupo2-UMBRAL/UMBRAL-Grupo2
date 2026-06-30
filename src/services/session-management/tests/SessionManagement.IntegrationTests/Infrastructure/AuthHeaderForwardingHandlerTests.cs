using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using SessionManagement.Infrastructure;
using Xunit;

namespace SessionManagement.IntegrationTests.Infrastructure;

public sealed class AuthHeaderForwardingHandlerTests
{
    private static HttpContextAccessor AccessorWithAuthorization(string? authorizationHeader)
    {
        var context = new DefaultHttpContext();
        if (authorizationHeader is not null)
        {
            context.Request.Headers.Authorization = authorizationHeader;
        }

        return new HttpContextAccessor { HttpContext = context };
    }

    private static AuthHeaderForwardingHandler CreateHandler(
        IHttpContextAccessor accessor,
        CapturingInnerHandler inner)
    {
        return new AuthHeaderForwardingHandler(accessor) { InnerHandler = inner };
    }

    [Fact]
    public async Task SendAsync_ForwardsAuthorizationHeaderFromHttpContext()
    {
        var inner = new CapturingInnerHandler();
        var handler = CreateHandler(AccessorWithAuthorization("Bearer incoming-token"), inner);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://downstream.test/resource");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.NotNull(inner.CapturedAuthorization);
        Assert.Equal("Bearer", inner.CapturedAuthorization!.Scheme);
        Assert.Equal("incoming-token", inner.CapturedAuthorization.Parameter);
    }

    [Fact]
    public async Task SendAsync_NoHttpContext_DoesNotSetAuthorization()
    {
        var inner = new CapturingInnerHandler();
        var accessor = new HttpContextAccessor { HttpContext = null };
        var handler = CreateHandler(accessor, inner);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://downstream.test/resource");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Null(inner.CapturedAuthorization);
    }

    [Fact]
    public async Task SendAsync_HttpContextWithoutAuthorizationHeader_DoesNotSetAuthorization()
    {
        var inner = new CapturingInnerHandler();
        var handler = CreateHandler(AccessorWithAuthorization(authorizationHeader: null), inner);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://downstream.test/resource");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Null(inner.CapturedAuthorization);
    }

    [Fact]
    public async Task SendAsync_DoesNotOverwriteExistingOutgoingAuthorization()
    {
        var inner = new CapturingInnerHandler();
        var handler = CreateHandler(AccessorWithAuthorization("Bearer incoming-token"), inner);
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://downstream.test/resource")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", "preset-token") }
        };

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.NotNull(inner.CapturedAuthorization);
        Assert.Equal("preset-token", inner.CapturedAuthorization!.Parameter);
    }

    private sealed class CapturingInnerHandler : HttpMessageHandler
    {
        public AuthenticationHeaderValue? CapturedAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CapturedAuthorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
