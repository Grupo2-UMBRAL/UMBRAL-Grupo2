using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Umbral.ServiceDefaults;
using UserManagement.Application.Common.Dtos;
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

    // Passwordless creation: no credential is set and the email stays unverified, so the account
    // cannot sign in until the onboarding invitation is completed. Absent names avoid overwriting the
    // profile the Operator fills in during UPDATE_PROFILE.
    [Fact]
    public async Task CreateUserAsync_WithoutPassword_OmitsCredentials_AndLeavesEmailUnverified()
    {
        var body = string.Empty;
        var newId = Guid.NewGuid().ToString();
        var (client, _) = BuildClient(request =>
        {
            if (IsToken(request))
            {
                return Token();
            }

            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.Location = new Uri($"http://keycloak:8080/admin/realms/{Realm}/users/{newId}");
            return response;
        });

        var result = await client.CreateUserAsync("jdoe", "jdoe@x.com", null, null, null, CancellationToken.None);

        Assert.Equal(newId, result);
        Assert.Contains("\"emailVerified\":false", body, StringComparison.Ordinal);
        Assert.DoesNotContain("credentials", body, StringComparison.Ordinal);
        Assert.DoesNotContain("firstName", body, StringComparison.Ordinal);
        Assert.DoesNotContain("lastName", body, StringComparison.Ordinal);
    }

    // A supplied password keeps the legacy behaviour self-registering Participants rely on: a permanent
    // credential and a pre-verified email.
    [Fact]
    public async Task CreateUserAsync_WithPassword_IncludesCredentials_AndVerifiesEmail()
    {
        var body = string.Empty;
        var newId = Guid.NewGuid().ToString();
        var (client, _) = BuildClient(request =>
        {
            if (IsToken(request))
            {
                return Token();
            }

            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.Location = new Uri($"http://keycloak:8080/admin/realms/{Realm}/users/{newId}");
            return response;
        });

        await client.CreateUserAsync("jdoe", "jdoe@x.com", "John", "Doe", "pw123456", CancellationToken.None);

        Assert.Contains("\"emailVerified\":true", body, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"password\"", body, StringComparison.Ordinal);
        Assert.Contains("\"value\":\"pw123456\"", body, StringComparison.Ordinal);
        Assert.Contains("\"firstName\":\"John\"", body, StringComparison.Ordinal);
        Assert.Contains("\"lastName\":\"Doe\"", body, StringComparison.Ordinal);
    }

    // The onboarding invitation is a PUT to execute-actions-email carrying exactly the required actions,
    // scoped to the web client and redirect so the link lands back inside UMBRAL.
    [Fact]
    public async Task SendOperatorOnboardingInvitationAsync_PutsExecuteActionsEmail_WithActionsAndRedirect()
    {
        var body = string.Empty;
        var options = new KeycloakAdminApiOptions
        {
            BaseUrl = "http://keycloak:8080",
            Realm = Realm,
            AdminRealm = AdminRealm,
            AdminClientId = "admin-cli",
            AdminUsername = "admin",
            AdminPassword = "pw",
            WebClientId = "umbral-web",
            OnboardingRedirectUri = "http://localhost:3000"
        };
        var (client, handler) = BuildClient(
            request =>
            {
                if (IsToken(request))
                {
                    return Token();
                }

                body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            },
            options);

        await client.SendOperatorOnboardingInvitationAsync("u1", CancellationToken.None);

        var invitation = Assert.Single(
            handler.Requests,
            r => r.Method == HttpMethod.Put &&
                 r.RequestUri!.AbsolutePath.EndsWith(
                     $"/admin/realms/{Realm}/users/u1/execute-actions-email",
                     StringComparison.Ordinal));
        Assert.Contains("client_id=umbral-web", invitation.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("redirect_uri=http", invitation.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("UPDATE_PASSWORD", body, StringComparison.Ordinal);
        Assert.Contains("UPDATE_PROFILE", body, StringComparison.Ordinal);
        Assert.Contains("VERIFY_EMAIL", body, StringComparison.Ordinal);
    }

    // With no redirect configured the link falls back to Keycloak's account console: still a valid PUT,
    // but without a client_id/redirect_uri that would need a whitelisted client to honour.
    [Fact]
    public async Task SendOperatorOnboardingInvitationAsync_WithoutRedirect_OmitsClientQuery()
    {
        var (client, handler) = BuildClient(
            request => IsToken(request) ? Token() : new HttpResponseMessage(HttpStatusCode.NoContent));

        await client.SendOperatorOnboardingInvitationAsync("u1", CancellationToken.None);

        var invitation = Assert.Single(
            handler.Requests,
            r => r.Method == HttpMethod.Put &&
                 r.RequestUri!.AbsolutePath.EndsWith(
                     $"/admin/realms/{Realm}/users/u1/execute-actions-email",
                     StringComparison.Ordinal));
        Assert.DoesNotContain("client_id", invitation.RequestUri!.Query, StringComparison.Ordinal);
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
    public async Task SendOperatorPasswordResetAsync_PutsExecuteActionsEmail_WithOnlyPasswordAction()
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

        await client.SendOperatorPasswordResetAsync("u1", CancellationToken.None);

        Assert.Single(
            handler.Requests,
            r => r.Method == HttpMethod.Put &&
                 r.RequestUri!.AbsolutePath.EndsWith(
                     $"/admin/realms/{Realm}/users/u1/execute-actions-email",
                     StringComparison.Ordinal));
        Assert.Equal("[\"UPDATE_PASSWORD\"]", putBody);
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
    public async Task GetParticipantProfileAsync_MapsRepresentation_ToSelfServiceFields()
    {
        const string userJson = """{"id":"u1","username":"pao.rojas","email":"pao.rojas@example.cl","firstName":"pao.rojas","lastName":"Jugador","enabled":true}""";
        var (client, handler) = BuildClient(request =>
            IsToken(request) ? Token() : Json(HttpStatusCode.OK, userJson));

        var result = await client.GetParticipantProfileAsync("u1", CancellationToken.None);

        Assert.Contains(handler.Requests, r =>
            r.Method == HttpMethod.Get &&
            r.RequestUri!.AbsolutePath.EndsWith($"/admin/realms/{Realm}/users/u1", StringComparison.Ordinal));
        Assert.Equal(new ParticipantProfileDto("u1", "pao.rojas", "pao.rojas@example.cl", true), result);
    }

    // A representation missing its username cannot produce a usable profile, and the screen shows the
    // username as the handle the player logs in with. Surfacing that as a DTO full of empty strings
    // would render a blank profile as if it were the truth, so it fails loudly instead.
    [Fact]
    public async Task GetParticipantProfileAsync_RepresentationWithoutUsername_Throws_Incomplete()
    {
        var (client, _) = BuildClient(request =>
            IsToken(request) ? Token() : Json(HttpStatusCode.OK, """{"id":"u1","enabled":true}"""));

        var ex = await Assert.ThrowsAsync<UmbralTechnicalException>(
            () => client.GetParticipantProfileAsync("u1", CancellationToken.None));

        Assert.Equal("participant_representation_incomplete", ex.Code);
    }

    [Fact]
    public async Task UpdateUsernameAsync_PutsUsernameAlone_AndReturnsTheUpdatedProfile()
    {
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
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return Json(
                HttpStatusCode.OK,
                """{"id":"u1","username":"nuevo.nombre","email":"pao@example.cl","enabled":true}""");
        });

        var result = await client.UpdateUsernameAsync("u1", "nuevo.nombre", CancellationToken.None);

        Assert.Contains(handler.Requests, r =>
            r.Method == HttpMethod.Put &&
            r.RequestUri!.AbsolutePath.EndsWith($"/admin/realms/{Realm}/users/u1", StringComparison.Ordinal));
        // Keycloak merges partial representations, so the payload must carry the username and nothing
        // else: sending "enabled" here would let a rename silently resurrect a deactivated account.
        Assert.Equal("""{"username":"nuevo.nombre"}""", putBody);
        Assert.Equal("nuevo.nombre", result.Username);
    }

    // SendAuthorizedAsync used to map every 409 from every call to operator_provider_duplicate. A
    // player renaming into a taken handle would have been told, in Operator vocabulary, that a
    // provider reported a duplicate.
    [Fact]
    public async Task UpdateUsernameAsync_Conflict_Throws_InParticipantVocabulary()
    {
        var (client, _) = BuildClient(request =>
            IsToken(request) ? Token() : new HttpResponseMessage(HttpStatusCode.Conflict));

        var ex = await Assert.ThrowsAsync<UmbralDomainException>(
            () => client.UpdateUsernameAsync("u1", "taken", CancellationToken.None));

        Assert.Equal("participant_username_taken", ex.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, ex.Category);
    }

    [Fact]
    public async Task FindUserIdByUsernameAsync_ReturnsHolderId_WhenTaken()
    {
        var (client, handler) = BuildClient(request =>
            IsToken(request)
                ? Token()
                : Json(HttpStatusCode.OK, """[{"id":"u9","username":"pao.rojas","enabled":true}]"""));

        var result = await client.FindUserIdByUsernameAsync("pao.rojas", CancellationToken.None);

        Assert.Equal("u9", result);
        Assert.Contains(handler.Requests, r =>
            r.Method == HttpMethod.Get &&
            r.RequestUri!.Query.Contains("username=pao.rojas", StringComparison.Ordinal) &&
            r.RequestUri!.Query.Contains("exact=true", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FindUserIdByUsernameAsync_ReturnsNull_WhenFree()
    {
        var (client, _) = BuildClient(request =>
            IsToken(request) ? Token() : Json(HttpStatusCode.OK, "[]"));

        var result = await client.FindUserIdByUsernameAsync("libre", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DeactivateParticipantAsync_PutsEnabledFalse_AndNothingElse()
    {
        var putBody = string.Empty;
        var (client, handler) = BuildClient(request =>
        {
            if (IsToken(request))
            {
                return Token();
            }

            putBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        await client.DeactivateParticipantAsync("u1", CancellationToken.None);

        Assert.Single(
            handler.Requests,
            r => r.Method == HttpMethod.Put &&
                 r.RequestUri!.AbsolutePath.EndsWith($"/admin/realms/{Realm}/users/u1", StringComparison.Ordinal));
        Assert.Equal("""{"enabled":false}""", putBody);
    }

    [Fact]
    public async Task LogoutParticipantSessionsAsync_PostsToTheLogoutEndpoint()
    {
        var (client, handler) = BuildClient(request =>
            IsToken(request) ? Token() : new HttpResponseMessage(HttpStatusCode.NoContent));

        await client.LogoutParticipantSessionsAsync("u1", CancellationToken.None);

        Assert.Single(
            handler.Requests,
            r => r.Method == HttpMethod.Post &&
                 r.RequestUri!.AbsolutePath.EndsWith(
                     $"/admin/realms/{Realm}/users/u1/logout",
                     StringComparison.Ordinal));
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
