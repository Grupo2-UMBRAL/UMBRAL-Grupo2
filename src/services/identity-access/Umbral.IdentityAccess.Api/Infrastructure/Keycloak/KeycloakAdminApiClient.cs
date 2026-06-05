using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Umbral.IdentityAccess.Api.Application.Operators;
using Umbral.ServiceDefaults;

namespace Umbral.IdentityAccess.Api.Infrastructure.Keycloak;

public sealed class KeycloakAdminApiClient(HttpClient httpClient, IOptions<KeycloakAdminApiOptions> options)
    : IOperatorAdministrationPort, IDisposable
{
    private readonly HttpClient httpClient = httpClient;
    private readonly KeycloakAdminApiOptions options = options.Value;
    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private string? cachedAccessToken;
    private DateTimeOffset accessTokenExpiresAtUtc;

    public async Task<IReadOnlyList<OperatorUser>> ListOperatorsAsync(CancellationToken cancellationToken)
    {
        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/admin/realms/{options.Realm}/roles/{options.OperatorRoleName}/users?briefRepresentation=false",
            cancellationToken: cancellationToken);
        var users = await ReadJsonAsync<List<KeycloakUserRepresentation>>(response, cancellationToken);

        return users.Select(ToOperatorUser).ToArray();
    }

    public async Task<IReadOnlyList<OperatorUser>> FindUsersByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var response = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/admin/realms/{options.Realm}/users?email={Uri.EscapeDataString(email)}&exact=true",
            cancellationToken: cancellationToken);
        var users = await ReadJsonAsync<List<KeycloakUserRepresentation>>(response, cancellationToken);

        return users
            .Where(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            .Select(ToOperatorUser)
            .ToArray();
    }

    public async Task<IReadOnlyList<OperatorUser>> FindUsersByUsernameAsync(
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
            .Select(ToOperatorUser)
            .ToArray();
    }

    public async Task<CreatedUserReference> CreateUserAsync(
        ValidatedCreateOperatorInput input,
        CancellationToken cancellationToken)
    {
        var response = await SendAuthorizedAsync(
            HttpMethod.Post,
            $"/admin/realms/{options.Realm}/users",
            new
            {
                username = input.Username,
                email = input.Email,
                firstName = input.FirstName,
                lastName = input.LastName,
                enabled = true,
                emailVerified = true,
                credentials = new[]
                {
                    new
                    {
                        type = "password",
                        value = input.Password,
                        temporary = false
                    }
                }
            },
            cancellationToken: cancellationToken);

        var createdUserId = response.Headers.Location?.Segments.LastOrDefault()?.Trim('/');

        if (!string.IsNullOrWhiteSpace(createdUserId))
        {
            return new CreatedUserReference(createdUserId);
        }

        var matchingUsers = await FindUsersByUsernameAsync(input.Username, cancellationToken);
        var recoveredUser = matchingUsers.FirstOrDefault();

        if (recoveredUser is null)
        {
            throw new UmbralTechnicalException(
                "operator_create_location_missing",
                "Keycloak created User without location header.");
        }

        return new CreatedUserReference(recoveredUser.Id);
    }

    public async Task AssignOperatorRoleAsync(string userId, CancellationToken cancellationToken)
    {
        var roleResponse = await SendAuthorizedAsync(
            HttpMethod.Get,
            $"/admin/realms/{options.Realm}/roles/{options.OperatorRoleName}",
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

    public async Task<OperatorUser?> GetUserByIdAsync(string userId, CancellationToken cancellationToken)
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
            return ToOperatorUser(user);
        }
    }

    public async Task<OperatorUser> SetUserEnabledAsync(
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

    public async Task RotateOperatorPasswordAsync(
        string userId,
        string password,
        CancellationToken cancellationToken)
    {
        var updateResponse = await SendAuthorizedAsync(
            HttpMethod.Put,
            $"/admin/realms/{options.Realm}/users/{Uri.EscapeDataString(userId)}/reset-password",
            new KeycloakCredentialRepresentation
            {
                Type = "password",
                Value = password,
                Temporary = false
            },
            cancellationToken: cancellationToken);
        updateResponse.Dispose();
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

    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpMethod method,
        string path,
        object? payload = null,
        bool allowNotFound = false,
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
                "operator_provider_duplicate",
                detail.Length == 0 ? "Keycloak reported a duplicate User." : detail,
                UmbralFailureCategory.Conflict);
        }

        throw new UmbralTechnicalException(
            "identity_access_keycloak_admin_failed",
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
                    "identity_access_admin_session_failed",
                    $"Could not open Keycloak admin session: {(detail.Length == 0 ? $"{(int)response.StatusCode} {response.ReasonPhrase}" : detail)}.");
            }

            using (response)
            {
                var payload = await ReadJsonAsync<KeycloakTokenResponse>(response, cancellationToken);

                if (string.IsNullOrWhiteSpace(payload.AccessToken))
                {
                    throw new UmbralTechnicalException(
                        "identity_access_admin_token_missing",
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
            throw new InvalidOperationException("Missing required configuration value 'IdentityAccess:Keycloak:BaseUrl'.");
        }

        if (string.IsNullOrWhiteSpace(options.Realm))
        {
            throw new InvalidOperationException("Missing required configuration value 'IdentityAccess:Keycloak:Realm'.");
        }

        if (string.IsNullOrWhiteSpace(options.AdminRealm))
        {
            throw new InvalidOperationException("Missing required configuration value 'IdentityAccess:Keycloak:AdminRealm'.");
        }

        if (string.IsNullOrWhiteSpace(options.AdminClientId))
        {
            throw new InvalidOperationException("Missing required configuration value 'IdentityAccess:Keycloak:AdminClientId'.");
        }

        if (string.IsNullOrWhiteSpace(options.AdminUsername) || string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            throw new UmbralTechnicalException(
                "identity_access_admin_credentials_missing",
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
                "identity_access_provider_payload_missing",
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

    private static OperatorUser ToOperatorUser(KeycloakUserRepresentation user) =>
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
