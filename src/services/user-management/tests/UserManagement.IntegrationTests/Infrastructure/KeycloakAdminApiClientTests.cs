using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Umbral.ServiceDefaults;
using UserManagement.Infrastructure.Keycloak;
using Xunit;

namespace UserManagement.IntegrationTests.Infrastructure;

public sealed class KeycloakAdminApiClientTests
{
    private const string Realm = "umbral";
    private const string AdminRealm = "master";
    private const string TokenJson = """{"access_token":"admin-token","expires_in":300}""";

    private static KeycloakAdminApiOptions Options_(
        string baseUrl = "http://keycloak:8080",
        string realm = Realm,
        string adminRealm = AdminRealm,
        string adminClientId = "admin-cli",
        string adminUsername = "admin",
        string adminPassword = "pw") => new()
    {
        BaseUrl = baseUrl,
        Realm = realm,
        AdminRealm = adminRealm,
        AdminClientId = adminClientId,
        AdminUsername = adminUsername,
        AdminPassword = adminPassword
    };

    private static KeycloakAdminApiOptions ValidOptions() => Options_();

    private static (KeycloakAdminApiClient Client, RoutingHttpMessageHandler Handler) BuildClient(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        KeycloakAdminApiOptions? options = null)
    {
        var handler = new RoutingHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler);
        var client = new KeycloakAdminApiClient(httpClient, Options.Create(options ?? ValidOptions()));
        return (client, handler);
    }

    private static bool IsToken(HttpRequestMessage request) =>
        request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Token() => Json(HttpStatusCode.OK, TokenJson);

    [Fact]
    public async Task GetUserByIdAsync_FetchesAdminTokenOnce_AndReusesIt()
    {
        const string userJson = """{"id":"u1","username":"jdoe","email":"jdoe@x.com","firstName":"John","lastName":"Doe","enabled":true}""";
        var (client, handler) = BuildClient(request =>
            IsToken(request) ? Token() : Json(HttpStatusCode.OK, userJson));

        await client.GetUserByIdAsync("u1", CancellationToken.None);
        await client.GetUserByIdAsync("u1", CancellationToken.None);

        var tokenRequests = handler.Requests.Count(IsToken);
        Assert.Equal(1, tokenRequests);
    }

    [Fact]
    public async Task CreateUserAsync_WithLocationHeader_ReturnsCreatedUserReference()
    {
        var newId = Guid.NewGuid().ToString();
        var (client, _) = BuildClient(request =>
        {
            if (IsToken(request))
            {
                return Token();
            }

            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.Location = new Uri($"http://keycloak:8080/admin/realms/{Realm}/users/{newId}");
            return response;
        });

        var result = await client.CreateUserAsync("jdoe", "jdoe@x.com", "John", "Doe", "pw123456", CancellationToken.None);

        Assert.Equal(newId, result);
    }

    [Fact]
    public async Task CreateUserAsync_NoLocation_RecoversIdViaUsernameLookup()
    {
        var recoveredId = Guid.NewGuid().ToString();
        var (client, handler) = BuildClient(request =>
        {
            if (IsToken(request))
            {
                return Token();
            }

            if (request.Method == HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.Created);
            }

            // GET users?username=...&exact=true
            return Json(
                HttpStatusCode.OK,
                $$"""[{"id":"{{recoveredId}}","username":"jdoe","email":"jdoe@x.com","firstName":"John","lastName":"Doe","enabled":true}]""");
        });

        var result = await client.CreateUserAsync("jdoe", "jdoe@x.com", "John", "Doe", "pw123456", CancellationToken.None);

        Assert.Equal(recoveredId, result);
        Assert.Contains(handler.Requests, r =>
            r.Method == HttpMethod.Get &&
            r.RequestUri!.AbsolutePath.EndsWith($"/admin/realms/{Realm}/users", StringComparison.Ordinal) &&
            r.RequestUri!.Query.Contains("username=jdoe", StringComparison.Ordinal) &&
            r.RequestUri!.Query.Contains("exact=true", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateUserAsync_NoLocation_AndEmptyLookup_Throws_LocationMissing()
    {
        var (client, _) = BuildClient(request =>
        {
            if (IsToken(request))
            {
                return Token();
            }

            if (request.Method == HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.Created);
            }

            return Json(HttpStatusCode.OK, "[]");
        });

        var ex = await Assert.ThrowsAsync<UmbralTechnicalException>(
            () => client.CreateUserAsync("jdoe", "jdoe@x.com", "John", "Doe", "pw123456", CancellationToken.None));

        Assert.Equal("operator_create_location_missing", ex.Code);
    }

    [Fact]
    public async Task CreateUserAsync_Conflict_Throws_ProviderDuplicate()
    {
        var (client, _) = BuildClient(request =>
            IsToken(request) ? Token() : new HttpResponseMessage(HttpStatusCode.Conflict));

        var ex = await Assert.ThrowsAsync<UmbralDomainException>(
            () => client.CreateUserAsync("jdoe", "jdoe@x.com", "John", "Doe", "pw123456", CancellationToken.None));

        Assert.Equal("operator_provider_duplicate", ex.Code);
    }

    [Fact]
    public async Task TokenEndpoint_Unauthorized_Throws_AdminSessionFailed()
    {
        var (client, _) = BuildClient(request =>
            IsToken(request)
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : Json(HttpStatusCode.OK, "{}"));

        var ex = await Assert.ThrowsAsync<UmbralTechnicalException>(
            () => client.GetUserByIdAsync("u1", CancellationToken.None));

        Assert.Equal("user_management_admin_session_failed", ex.Code);
    }

    [Fact]
    public async Task SetUserEnabledAsync_PutsUser_WithEnabledFlag_AndReturnsUpdatedUser()
    {
        var enabledStateBeforeUpdate = false;
        var putBody = string.Empty;

        var (client, handler) = BuildClient(request =>
        {
            if (IsToken(request))
            {
                return Token();
            }

            if (request.Method == HttpMethod.Put)
            {
                putBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                enabledStateBeforeUpdate = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            // GET user-by-id: reflect enabled flag after the PUT happened
            var enabled = enabledStateBeforeUpdate ? "true" : "false";
            return Json(
                HttpStatusCode.OK,
                $$"""{"id":"u1","username":"jdoe","email":"jdoe@x.com","firstName":"John","lastName":"Doe","enabled":{{enabled}}}""");
        });

        var result = await client.SetUserEnabledAsync("u1", true, CancellationToken.None);

        Assert.Contains(handler.Requests, r =>
            r.Method == HttpMethod.Put &&
            r.RequestUri!.AbsolutePath.EndsWith($"/admin/realms/{Realm}/users/u1", StringComparison.Ordinal));
        Assert.Contains("\"enabled\"", putBody, StringComparison.Ordinal);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task RotateOperatorPasswordAsync_PutsResetPassword_WithPasswordType()
    {
        var putBody = string.Empty;
        var (client, handler) = BuildClient(request =>
        {
            if (IsToken(request))
            {
                return Token();
            }

            putBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await client.RotateOperatorPasswordAsync("u1", "newpw123456", CancellationToken.None);

        Assert.Single(
            handler.Requests,
            r => r.Method == HttpMethod.Put &&
                 r.RequestUri!.AbsolutePath.EndsWith(
                     $"/admin/realms/{Realm}/users/u1/reset-password",
                     StringComparison.Ordinal));
        Assert.Contains("\"type\":\"password\"", putBody, StringComparison.Ordinal);
        Assert.Contains("\"temporary\":false", putBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssignOperatorRoleAsync_PostsRoleArray_ToRealmMappings()
    {
        var postBody = string.Empty;
        var (client, handler) = BuildClient(request =>
        {
            if (IsToken(request))
            {
                return Token();
            }

            if (request.Method == HttpMethod.Get)
            {
                return Json(HttpStatusCode.OK, """{"id":"role-1","name":"operator"}""");
            }

            postBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        await client.AssignOperatorRoleAsync("u1", CancellationToken.None);

        Assert.Single(
            handler.Requests,
            r => r.Method == HttpMethod.Post &&
                 r.RequestUri!.AbsolutePath.EndsWith(
                     $"/admin/realms/{Realm}/users/u1/role-mappings/realm",
                     StringComparison.Ordinal));
        Assert.StartsWith("[", postBody.TrimStart(), StringComparison.Ordinal);
        Assert.Contains("role-1", postBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetUserByIdAsync_NotFound_ReturnsNull()
    {
        var (client, _) = BuildClient(request =>
            IsToken(request) ? Token() : new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await client.GetUserByIdAsync("missing", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Config_EmptyBaseUrl_Throws_InvalidOperation()
    {
        var options = Options_(baseUrl: string.Empty);
        var (client, _) = BuildClient(_ => Token(), options);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetUserByIdAsync("u1", CancellationToken.None));
    }

    [Fact]
    public async Task Config_EmptyRealm_Throws_InvalidOperation()
    {
        var options = Options_(realm: string.Empty);
        var (client, _) = BuildClient(_ => Token(), options);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetUserByIdAsync("u1", CancellationToken.None));
    }

    [Fact]
    public async Task Config_EmptyAdminRealm_Throws_InvalidOperation()
    {
        var options = Options_(adminRealm: string.Empty);
        var (client, _) = BuildClient(_ => Token(), options);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetUserByIdAsync("u1", CancellationToken.None));
    }

    [Fact]
    public async Task Config_EmptyAdminClientId_Throws_InvalidOperation()
    {
        var options = Options_(adminClientId: string.Empty);
        var (client, _) = BuildClient(_ => Token(), options);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetUserByIdAsync("u1", CancellationToken.None));
    }

    [Fact]
    public async Task Config_EmptyAdminUsername_Throws_CredentialsMissing()
    {
        var options = Options_(adminUsername: string.Empty);
        var (client, _) = BuildClient(_ => Token(), options);

        var ex = await Assert.ThrowsAsync<UmbralTechnicalException>(
            () => client.GetUserByIdAsync("u1", CancellationToken.None));

        Assert.Equal("user_management_admin_credentials_missing", ex.Code);
    }

    [Theory]
    [InlineData("{\"error_description\": \"Detailed error\"}", "Detailed error")]
    [InlineData("{\"error\": \"Short error\"}", "Short error")]
    [InlineData("{\"message\": \"Generic message\"}", "Generic message")]
    [InlineData("{\"other\": \"value\"}", "{\"other\": \"value\"}")]
    [InlineData("not json", "not json")]
    [InlineData("", "400 Bad Request")]
    public async Task SendAuthorizedAsync_Failure_ReadsFailureDetail(string body, string expectedMessage)
    {
        var (client, _) = BuildClient(request =>
            IsToken(request) ? Token() : Json(HttpStatusCode.BadRequest, body));

        var ex = await Assert.ThrowsAsync<UmbralTechnicalException>(
            () => client.GetUserByIdAsync("u1", CancellationToken.None));

        Assert.Contains(expectedMessage, ex.Message);
    }
}
