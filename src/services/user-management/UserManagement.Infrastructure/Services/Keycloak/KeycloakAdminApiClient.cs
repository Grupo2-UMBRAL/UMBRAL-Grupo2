using UserManagement.Application.Abstractions;
using UserManagement.Application.Common.Dtos;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Umbral.ServiceDefaults;

namespace UserManagement.Infrastructure.Keycloak;

public sealed class KeycloakAdminApiClient(HttpClient httpClient, IOptions<KeycloakAdminApiOptions> options)
    : IOperatorAdministrationPort, IParticipantAdministrationPort, IDisposable
{
    private readonly HttpClient httpClient = httpClient;
    private readonly KeycloakAdminApiOptions options = options.Value;
    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private string? cachedAccessToken;
    private DateTimeOffset accessTokenExpiresAtUtc;

    public async Task<IReadOnlyList<OperatorDto>> ListOperatorsAsync(CancellationToken cancellationToken)
    {
        const int pageSize = 100;
        var users = new List<KeycloakUserRepresentation>();

        for (var first = 0; ; first += pageSize)
        {
            var response = await SendAuthorizedAsync(
                HttpMethod.Get,
                $"/admin/realms/{options.Realm}/roles/{options.OperatorRoleName}/users?briefRepresentation=false&first={first}&max={pageSize}",
                cancellationToken: cancellationToken);
            var page = await ReadJsonAsync<List<KeycloakUserRepresentation>>(response, cancellationToken);
            users.AddRange(page);

            if (page.Count < pageSize)
            {
                return users.Select(ToOperatorDto).ToArray();
            }
        }
    }

    public async Task<IReadOnlyList<OperatorDto>> FindUsersByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/admin/realms/{options.Realm}/users?email={Uri.EscapeDataString(email)}&exact=true",
            cancellationToken: cancellationToken);
        var users = await ReadJsonAsync<List<KeycloakUserRepresentation>>(response, cancellationToken);

        return users
            .Where(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            .Select(ToOperatorDto)
            .ToArray();
    }

    public async Task<IReadOnlyList<OperatorDto>> FindUsersByUsernameAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/admin/realms/{options.Realm}/users?username={Uri.EscapeDataString(username)}&exact=true",
            cancellationToken: cancellationToken);
        var users = await ReadJsonAsync<List<KeycloakUserRepresentation>>(response, cancellationToken);

        return users
            .Where(user => string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase))
            .Select(ToOperatorDto)
            .ToArray();
    }

    public async Task<string> CreateUserAsync(
        string username,
        string email,
        string? firstName,
        string? lastName,
        string? password,
        CancellationToken cancellationToken)
    {
        // Keycloak merges only the keys we send. A passwordless account is created with no credential
        // and emailVerified=false so it cannot sign in until the onboarding invitation is completed;
        // the VERIFY_EMAIL action then flips the flag. Passing a password keeps the legacy verified,
        // permanently-credentialed account that self-registering Participants depend on.
        var payload = new Dictionary<string, object?>
        {
            ["username"] = username,
            ["email"] = email,
            ["enabled"] = true,
            ["emailVerified"] = !string.IsNullOrWhiteSpace(password)
        };

        if (!string.IsNullOrWhiteSpace(firstName))
        {
            payload["firstName"] = firstName;
        }

        if (!string.IsNullOrWhiteSpace(lastName))
        {
            payload["lastName"] = lastName;
        }

        if (!string.IsNullOrWhiteSpace(password))
        {
            payload["credentials"] = new[]
            {
                new
                {
                    type = "password",
                    value = password,
                    temporary = false
                }
            };
        }

        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/admin/realms/{options.Realm}/users",
            payload,
            cancellationToken: cancellationToken);

        var createdUserId = response.Headers.Location?.Segments.LastOrDefault()?.Trim('/');

        if (!string.IsNullOrWhiteSpace(createdUserId))
        {
            return createdUserId;
        }

        var matchingUsers = await FindUsersByUsernameAsync(username, cancellationToken);
        var recoveredUser = matchingUsers.FirstOrDefault();

        if (recoveredUser is null)
        {
            throw new UmbralTechnicalException(
                "operator_create_location_missing",
                "Keycloak created User without location header.");
        }

        return recoveredUser.Id;
    }

    // The required actions Keycloak walks the Operator through, in the order they read best. Keycloak
    // owns the actual screen sequence; this is only the set to require.
    private static readonly string[] OnboardingRequiredActions =
        ["UPDATE_PASSWORD", "UPDATE_PROFILE", "VERIFY_EMAIL"];

    public async Task SendOperatorOnboardingInvitationAsync(string userId, CancellationToken cancellationToken)
        => await SendExecuteActionsEmailAsync(userId, OnboardingRequiredActions, cancellationToken);

    public async Task SendOperatorPasswordResetAsync(string userId, CancellationToken cancellationToken)
        => await SendExecuteActionsEmailAsync(userId, ["UPDATE_PASSWORD"], cancellationToken);

    private async Task SendExecuteActionsEmailAsync(
        string userId,
        IReadOnlyList<string> requiredActions,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();

        // client_id and redirect_uri travel together: a redirect is only honoured against a client, and
        // omitting both lets Keycloak fall back to its account console. lifespan overrides the realm default.
        if (!string.IsNullOrWhiteSpace(options.OnboardingRedirectUri))
        {
            query.Add($"client_id={Uri.EscapeDataString(options.WebClientId)}");
            query.Add($"redirect_uri={Uri.EscapeDataString(options.OnboardingRedirectUri)}");
        }

        if (options.OnboardingLinkLifespanSeconds > 0)
        {
            query.Add($"lifespan={options.OnboardingLinkLifespanSeconds}");
        }

        var path = $"/admin/realms/{options.Realm}/users/{Uri.EscapeDataString(userId)}/execute-actions-email";
        if (query.Count > 0)
        {
            path = $"{path}?{string.Join('&', query)}";
        }

        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            path,
            requiredActions,
            cancellationToken: cancellationToken);
        response.Dispose();
    }

    public Task AssignOperatorRoleAsync(string userId, CancellationToken cancellationToken)
        => AssignRealmRoleByNameAsync(userId, options.OperatorRoleName, cancellationToken);

    public Task AssignParticipantRoleAsync(string userId, CancellationToken cancellationToken)
        => AssignRealmRoleByNameAsync(userId, options.ParticipantRoleName, cancellationToken);

    private async Task AssignRealmRoleByNameAsync(string userId, string roleName, CancellationToken cancellationToken)
    {
        var roleResponse = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/admin/realms/{options.Realm}/roles/{roleName}",
            cancellationToken: cancellationToken);
        var role = await ReadJsonAsync<KeycloakRoleRepresentation>(roleResponse, cancellationToken);

        await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/admin/realms/{options.Realm}/users/{Uri.EscapeDataString(userId)}/role-mappings/realm",
            new[]
            {
                role
            },
            cancellationToken: cancellationToken);
    }

    public async Task DeleteUserAsync(string userId, CancellationToken cancellationToken)
    {
        var response = await SendAuthorizedAsync(
            HttpMethod.Delete,
            $"/admin/realms/{options.Realm}/users/{Uri.EscapeDataString(userId)}",
            cancellationToken: cancellationToken);

        response.Dispose();
    }

    public async Task<OperatorDto?> GetUserByIdAsync(string userId, CancellationToken cancellationToken)
    {
        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/admin/realms/{options.Realm}/users/{Uri.EscapeDataString(userId)}",
            allowNotFound: true,
            cancellationToken: cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            response.Dispose();
            return null;
        }

        using (response)
        {
            var user = await ReadJsonAsync<KeycloakUserRepresentation>(response, cancellationToken);
            return ToOperatorDto(user);
        }
    }

    public async Task<OperatorDto> SetUserEnabledAsync(
        string userId,
        bool enabled,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetUserRepresentationByIdAsync(userId, cancellationToken);
        currentUser.Enabled = enabled;

        var updateResponse = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/admin/realms/{options.Realm}/users/{Uri.EscapeDataString(userId)}",
            currentUser,
            cancellationToken: cancellationToken);
        updateResponse.Dispose();

        var updatedUser = await GetUserByIdAsync(userId, cancellationToken);

        if (updatedUser is null)
        {
            throw new UmbralTechnicalException(
                "operator_update_followup_failed",
                "Keycloak updated User but follow-up read failed.");
        }

        return updatedUser;
    }

    public async Task<ParticipantProfileDto> GetParticipantProfileAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        // No allowNotFound: the id is the `sub` of a token this realm just signed, so a 404 means the
        // realm contradicts its own token. That is a technical fault, not a 404 to hand the player.
        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/admin/realms/{options.Realm}/users/{Uri.EscapeDataString(userId)}",
            cancellationToken: cancellationToken);

        using (response)
        {
            var user = await ReadJsonAsync<KeycloakUserRepresentation>(response, cancellationToken);
            return ToParticipantProfileDto(user);
        }
    }

    public async Task<string?> FindUserIdByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/admin/realms/{options.Realm}/users?username={Uri.EscapeDataString(username)}&exact=true",
            cancellationToken: cancellationToken);
        var users = await ReadJsonAsync<List<KeycloakUserRepresentation>>(response, cancellationToken);

        return users
            .FirstOrDefault(user => string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase))
            ?.Id;
    }

    public async Task<ParticipantProfileDto> UpdateUsernameAsync(
        string userId,
        string username,
        CancellationToken cancellationToken)
    {
        // Keycloak merges a partial UserRepresentation on PUT, so sending username alone leaves email,
        // names, role mappings and the enabled flag untouched. Read-modify-write would only widen the
        // race with the handler's pre-check.
        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/admin/realms/{options.Realm}/users/{Uri.EscapeDataString(userId)}",
            new
            {
                username
            },
            conflictCode: "participant_username_taken",
            cancellationToken: cancellationToken);
        response.Dispose();

        return await GetParticipantProfileAsync(userId, cancellationToken);
    }

    public async Task DeactivateParticipantAsync(string userId, CancellationToken cancellationToken)
    {
        // Partial merge again: enabled alone, so deactivating cannot disturb the username or email.
        var response = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/admin/realms/{options.Realm}/users/{Uri.EscapeDataString(userId)}",
            new
            {
                enabled = false
            },
            cancellationToken: cancellationToken);
        response.Dispose();
    }

    public async Task LogoutParticipantSessionsAsync(string userId, CancellationToken cancellationToken)
    {
        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/admin/realms/{options.Realm}/users/{Uri.EscapeDataString(userId)}/logout",
            cancellationToken: cancellationToken);
        response.Dispose();
    }

    public void Dispose()
    {
        tokenLock.Dispose();
    }

    private async Task<KeycloakUserRepresentation> GetUserRepresentationByIdAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/admin/realms/{options.Realm}/users/{Uri.EscapeDataString(userId)}",
            allowNotFound: true,
            cancellationToken: cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            response.Dispose();
            throw new UmbralDomainException(
                "operator_user_not_found",
                "Operator User was not found.",
                UmbralFailureCategory.NotFound);
        }

        using (response)
        {
            return await ReadJsonAsync<KeycloakUserRepresentation>(response, cancellationToken);
        }
    }

    // conflictCode is the failure code for a provider 409. It defaults to the Operator vocabulary
    // because that is where every caller of this method started; participant routes pass their own,
    // so a player is never handed an operator_* code for a username they chose.
    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpMethod method,
        string path,
        object? payload = null,
        bool allowNotFound = false,
        string conflictCode = "operator_provider_duplicate",
        CancellationToken cancellationToken = default)
    {
        EnsureRequiredConfiguration();

        using var request = new HttpRequestMessage(method, BuildUri(path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAdminAccessTokenAsync(cancellationToken));

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (allowNotFound && response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return response;
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var detail = await ReadFailureDetailAsync(response, cancellationToken);
        response.Dispose();

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            throw new UmbralDomainException(
                conflictCode,
                detail.Length == 0 ? "Keycloak reported a duplicate User." : detail,
                UmbralFailureCategory.Conflict);
        }

        throw new UmbralTechnicalException(
            "user_management_keycloak_admin_failed",
            $"Keycloak admin API failed: {(detail.Length == 0 ? $"{(int)response.StatusCode} {response.ReasonPhrase}" : detail)}.");
    }

    private async Task<string> GetAdminAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(cachedAccessToken) &&
            accessTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddSeconds(30))
        {
            return cachedAccessToken;
        }

        await tokenLock.WaitAsync(cancellationToken);

        try
        {
            if (!string.IsNullOrWhiteSpace(cachedAccessToken) &&
                accessTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddSeconds(30))
            {
                return cachedAccessToken;
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                BuildUri($"/realms/{options.AdminRealm}/protocol/openid-connect/token"))
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["client_id"] = options.AdminClientId,
                    ["username"] = options.AdminUsername,
                    ["password"] = options.AdminPassword
                })
            };

            var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await ReadFailureDetailAsync(response, cancellationToken);
                response.Dispose();

                throw new UmbralTechnicalException(
                    "user_management_admin_session_failed",
                    $"Could not open Keycloak admin session: {(detail.Length == 0 ? $"{(int)response.StatusCode} {response.ReasonPhrase}" : detail)}.");
            }

            using (response)
            {
                var payload = await ReadJsonAsync<KeycloakTokenResponse>(response, cancellationToken);

                if (string.IsNullOrWhiteSpace(payload.AccessToken))
                {
                    throw new UmbralTechnicalException(
                        "user_management_admin_token_missing",
                        "Keycloak admin token response omitted access token.");
                }

                cachedAccessToken = payload.AccessToken;
                accessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(
                    payload.ExpiresIn > 0 ? payload.ExpiresIn : 60);

                return cachedAccessToken;
            }
        }
        finally
        {
            tokenLock.Release();
        }
    }

    private void EnsureRequiredConfiguration()
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException("Missing required configuration value 'UserManagement:Keycloak:BaseUrl'.");
        }

        if (string.IsNullOrWhiteSpace(options.Realm))
        {
            throw new InvalidOperationException("Missing required configuration value 'UserManagement:Keycloak:Realm'.");
        }

        if (string.IsNullOrWhiteSpace(options.AdminRealm))
        {
            throw new InvalidOperationException("Missing required configuration value 'UserManagement:Keycloak:AdminRealm'.");
        }

        if (string.IsNullOrWhiteSpace(options.AdminClientId))
        {
            throw new InvalidOperationException("Missing required configuration value 'UserManagement:Keycloak:AdminClientId'.");
        }

        if (string.IsNullOrWhiteSpace(options.AdminUsername) || string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            throw new UmbralTechnicalException(
                "user_management_admin_credentials_missing",
                "Missing Keycloak admin credentials for Operator management.");
        }
    }

    private Uri BuildUri(string path) => new($"{options.BaseUrl.TrimEnd('/')}{path}");

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);

        if (payload is null)
        {
            throw new UmbralTechnicalException(
                "user_management_provider_payload_missing",
                "Keycloak returned an empty payload.");
        }

        return payload;
    }

    private static async Task<string> ReadFailureDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            foreach (var propertyName in new[] { "error_description", "error", "message" })
            {
                if (root.TryGetProperty(propertyName, out var property) &&
                    property.ValueKind == JsonValueKind.String)
                {
                    var value = property.GetString();

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return text.Trim();
    }

    // ToOperatorDto coalesces every field away, which suits a list an Administrator scans. A profile
    // is read as fact by its owner, so an id-less or username-less representation fails here instead
    // of rendering a blank account as though it were the truth.
    private static ParticipantProfileDto ToParticipantProfileDto(KeycloakUserRepresentation user)
    {
        if (string.IsNullOrWhiteSpace(user.Id) || string.IsNullOrWhiteSpace(user.Username))
        {
            throw new UmbralTechnicalException(
                "participant_representation_incomplete",
                "Keycloak returned a User without an id or a username.");
        }

        return new ParticipantProfileDto(
            user.Id,
            user.Username,
            user.Email ?? string.Empty,
            user.Enabled ?? false);
    }

    private static OperatorDto ToOperatorDto(KeycloakUserRepresentation user) =>
        new(
            user.Id ?? string.Empty,
            user.Username ?? string.Empty,
            user.Email ?? string.Empty,
            user.FirstName ?? string.Empty,
            user.LastName ?? string.Empty,
            user.Enabled ?? false);

    private sealed class KeycloakUserRepresentation
    {
        public string? Id { get; set; }

        public string? Username { get; set; }

        public string? Email { get; set; }

        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public bool? Enabled { get; set; }

        public bool? EmailVerified { get; set; }

        public KeycloakCredentialRepresentation[]? Credentials { get; set; }
    }

    private sealed class KeycloakCredentialRepresentation
    {
        public string? Type { get; set; }

        public string? Value { get; set; }

        public bool Temporary { get; set; }
    }

    private sealed class KeycloakRoleRepresentation
    {
        public string? Id { get; set; }

        public string? Name { get; set; }
    }

    private sealed class KeycloakTokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
