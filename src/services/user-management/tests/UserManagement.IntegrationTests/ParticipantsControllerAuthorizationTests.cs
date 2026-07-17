using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UserManagement.Api.Controllers;
using Xunit;

namespace UserManagement.IntegrationTests;

/// <summary>
/// Locks the authorization surface of <see cref="ParticipantsController"/>. Both attributes used to
/// sit on the class, which is why this guard exists: participant self-service actions live on the
/// same controller as the anonymous registration endpoint, and the two must not share either
/// attribute.
/// </summary>
public sealed class ParticipantsControllerAuthorizationTests
{
    // AuthorizationMiddleware asks the endpoint for IAllowAnonymous and, finding one, skips
    // authorization entirely -- it never looks at where the attribute was declared. On the class it
    // therefore lands on every action's metadata, so an [Authorize] added to a new action guards
    // nothing and the endpoint ships anonymous. Same reasoning for the limiter: shared at class
    // level, self-service calls would drain the registration bucket for that IP, and vice versa.
    [Fact]
    public void ControllerClass_DeclaresNeitherAnonymousAccessNorRateLimiting()
    {
        var controller = typeof(ParticipantsController);

        Assert.Null(controller.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(controller.GetCustomAttribute<EnableRateLimitingAttribute>());
    }

    // The counterpart: moving them down must not have dropped them. Players hold no token when they
    // register, so this endpoint stays anonymous, and being anonymous is exactly why it stays rate
    // limited.
    [Fact]
    public void RegistrationEndpoint_IsAnonymous_AndKeepsItsOwnRateLimiter()
    {
        using var factory = new UserManagementApiFactory();
        _ = factory.CreateClient();

        var registration = factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Single(endpoint =>
                endpoint.RoutePattern.RawText == "api/participants" &&
                endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains("POST"));

        Assert.NotNull(registration.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.Equal(
            ParticipantSignupRateLimiter.PolicyName,
            registration.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);
    }

    // The behavioural half of the guard above, and the reason it was worth fixing first: with
    // [AllowAnonymous] still on the class every one of these answers 200 and acts on a stranger's
    // account. Each self-service endpoint added from here belongs in this list.
    [Theory]
    [InlineData("GET", "/api/participants/me")]
    [InlineData("PATCH", "/api/participants/me/username")]
    [InlineData("POST", "/api/participants/me/deactivate")]
    public async Task SelfServiceEndpoint_WithoutToken_IsRejected(string method, string path)
    {
        using var factory = new UserManagementApiFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
