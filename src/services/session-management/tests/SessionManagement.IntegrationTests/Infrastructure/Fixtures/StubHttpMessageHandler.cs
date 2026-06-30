using System.Net;

namespace SessionManagement.IntegrationTests.Infrastructure.Fixtures;

// Records the outbound request and returns a pre-configured response, or throws a
// pre-configured exception. Reuses the capturing style from the existing
// ScoringAuditHttpClientTests.CapturingHttpMessageHandler, extended so a single
// handler can also simulate transport failures (HttpRequestException/timeout).
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage? _response;
    private readonly Exception? _exception;

    private StubHttpMessageHandler(HttpResponseMessage? response, Exception? exception)
    {
        _response = response;
        _exception = exception;
    }

    public HttpMethod? RequestMethod { get; private set; }

    public string? RequestPath { get; private set; }

    public string RequestContent { get; private set; } = string.Empty;

    public static StubHttpMessageHandler Returns(HttpResponseMessage response)
        => new(response, null);

    public static StubHttpMessageHandler Returns(HttpStatusCode statusCode)
        => new(new HttpResponseMessage(statusCode), null);

    public static StubHttpMessageHandler Throws(Exception exception)
        => new(null, exception);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestMethod = request.Method;
        RequestPath = request.RequestUri?.AbsolutePath;
        RequestContent = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        if (_exception is not null)
        {
            throw _exception;
        }

        return _response!;
    }
}
