using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbral.IdentityAccess.Api.Application.Operators;
using Umbral.ServiceDefaults;
using Xunit;

namespace Umbral.IdentityAccess.Api.Tests;

public sealed class OperatorAdministrationServiceTests
{
    [Fact]
    public async Task CreateOperator_RejectsDuplicateEmailBeforeCreatingUser()
    {
        var port = new FakeOperatorAdministrationPort();
        port.Users.Add(new OperatorUser(
            "existing-1",
            "existing.operator",
            "operator@umbral.local",
            "Existing",
            "Operator",
            true));
        var service = new OperatorAdministrationService(port);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            service.CreateOperatorAsync(
                new CreateOperatorInput(
                    "new.operator",
                    "operator@umbral.local",
                    "New",
                    "Operator",
                    "operator123!"),
                CancellationToken.None));

        Assert.Equal("operator_email_duplicate", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
    }

    [Fact]
    public async Task CreateOperator_RollsBackCreatedUserWhenRoleAssignmentFails()
    {
        var port = new FakeOperatorAdministrationPort
        {
            FailRoleAssignment = true
        };
        var service = new OperatorAdministrationService(port);

        var exception = await Assert.ThrowsAsync<UmbralTechnicalException>(() =>
            service.CreateOperatorAsync(
                new CreateOperatorInput(
                    "new.operator",
                    "new.operator@umbral.local",
                    "New",
                    "Operator",
                    "operator123!"),
                CancellationToken.None));

        Assert.Equal("simulated_keycloak_role_failure", exception.Code);
        Assert.Equal(new[] { "user-1" }, port.DeletedUserIds);
        Assert.Empty(port.Users);
    }

    [Fact]
    public async Task DeactivateOperator_FlipsActiveOperatorToInactive()
    {
        var port = new FakeOperatorAdministrationPort();
        port.Users.Add(new OperatorUser(
            "operator-1",
            "field.operator",
            "field.operator@umbral.local",
            "Field",
            "Operator",
            true));
        var service = new OperatorAdministrationService(port);

        var updatedOperator = await service.DeactivateOperatorAsync("operator-1", CancellationToken.None);

        Assert.False(updatedOperator.IsActive);
    }

    [Fact]
    public async Task ListOperators_SortsActiveUsersBeforeInactiveUsers()
    {
        var port = new FakeOperatorAdministrationPort();
        port.Users.AddRange(
        [
            new OperatorUser(
                "operator-2",
                "zeta.operator",
                "zeta@umbral.local",
                "Zeta",
                "Operator",
                false),
            new OperatorUser(
                "operator-1",
                "alpha.operator",
                "alpha@umbral.local",
                "Alpha",
                "Operator",
                true)
        ]);
        var service = new OperatorAdministrationService(port);

        var operators = await service.ListOperatorsAsync(CancellationToken.None);

        Assert.Equal(
            new[] { "alpha.operator", "zeta.operator" },
            operators.Select(operatorUser => operatorUser.Username));
    }
}

public sealed class OperatorEndpointTests
{
    [Fact]
    public async Task ListOperators_ReturnsOrderedUsers()
    {
        await using var factory = new IdentityAccessApiFactory();
        factory.Port.Users.AddRange(
        [
            new OperatorUser(
                "operator-2",
                "zeta.operator",
                "zeta@umbral.local",
                "Zeta",
                "Operator",
                false),
            new OperatorUser(
                "operator-1",
                "alpha.operator",
                "alpha@umbral.local",
                "Alpha",
                "Operator",
                true)
        ]);
        var client = factory.CreateAuthorizedClient();

        var operators = await client.GetFromJsonAsync<List<OperatorUser>>("/api/identity-access/operators");

        Assert.NotNull(operators);
        Assert.Equal(
            new[] { "alpha.operator", "zeta.operator" },
            operators.Select(operatorUser => operatorUser.Username));
    }

    [Fact]
    public async Task CreateOperator_ReturnsCreatedUser()
    {
        await using var factory = new IdentityAccessApiFactory();
        var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsJsonAsync(
            "/api/identity-access/operators",
            new
            {
                Username = "new.operator",
                Email = "new.operator@umbral.local",
                FirstName = "New",
                LastName = "Operator",
                Password = "operator123!"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var operatorUser = await response.Content.ReadFromJsonAsync<OperatorUser>();

        Assert.NotNull(operatorUser);
        Assert.Equal("new.operator", operatorUser.Username);
        Assert.True(operatorUser.IsActive);
    }

    [Fact]
    public async Task DeactivateOperator_ReturnsInactiveUser()
    {
        await using var factory = new IdentityAccessApiFactory();
        factory.Port.Users.Add(new OperatorUser(
            "operator-1",
            "field.operator",
            "field.operator@umbral.local",
            "Field",
            "Operator",
            true));
        var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsync("/api/identity-access/operators/operator-1/deactivate", content: null);

        response.EnsureSuccessStatusCode();

        var operatorUser = await response.Content.ReadFromJsonAsync<OperatorUser>();

        Assert.NotNull(operatorUser);
        Assert.False(operatorUser.IsActive);
    }
}

internal sealed class IdentityAccessApiFactory : WebApplicationFactory<Program>
{
    private readonly string? previousAuthAuthority = Environment.GetEnvironmentVariable("Auth__Authority");
    private readonly string? previousAuthAudience = Environment.GetEnvironmentVariable("Auth__Audience");

    public FakeOperatorAdministrationPort Port { get; } = new();

    public IdentityAccessApiFactory()
    {
        Environment.SetEnvironmentVariable("Auth__Authority", "http://localhost:8080/realms/umbral");
        Environment.SetEnvironmentVariable("Auth__Audience", "umbral-identity-access-api");
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "http://localhost:8080/realms/umbral",
                ["Auth:Audience"] = "umbral-identity-access-api"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });

            services.RemoveAll<IOperatorAdministrationPort>();
            services.AddSingleton<IOperatorAdministrationPort>(Port);
        });
    }

    public HttpClient CreateAuthorizedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.RoleHeaderName, UmbralRoles.Administrator);

        return client;
    }

    public override async ValueTask DisposeAsync()
    {
        Environment.SetEnvironmentVariable("Auth__Authority", previousAuthAuthority);
        Environment.SetEnvironmentVariable("Auth__Audience", previousAuthAudience);
        await base.DisposeAsync();
    }

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string RoleHeaderName = "X-Test-Role";

        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(RoleHeaderName))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = Request.Headers[RoleHeaderName]
                .Where(static role => !string.IsNullOrWhiteSpace(role))
                .Select(role => new Claim(ClaimTypes.Role, role!));
            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

internal sealed class FakeOperatorAdministrationPort : IOperatorAdministrationPort
{
    public List<OperatorUser> Users { get; } = [];

    public List<string> DeletedUserIds { get; } = [];

    public bool FailRoleAssignment { get; set; }

    public Task<IReadOnlyList<OperatorUser>> ListOperatorsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OperatorUser>>(Users.ToArray());

    public Task<IReadOnlyList<OperatorUser>> FindUsersByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OperatorUser>>(
            Users.Where(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)).ToArray());

    public Task<IReadOnlyList<OperatorUser>> FindUsersByUsernameAsync(
        string username,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OperatorUser>>(
            Users.Where(user => string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase)).ToArray());

    public Task<CreatedUserReference> CreateUserAsync(
        ValidatedCreateOperatorInput input,
        CancellationToken cancellationToken)
    {
        var createdUser = new OperatorUser(
            $"user-{Users.Count + 1}",
            input.Username,
            input.Email,
            input.FirstName,
            input.LastName,
            true);

        Users.Add(createdUser);

        return Task.FromResult(new CreatedUserReference(createdUser.Id));
    }

    public Task AssignOperatorRoleAsync(string userId, CancellationToken cancellationToken)
    {
        if (FailRoleAssignment)
        {
            throw new UmbralTechnicalException(
                "simulated_keycloak_role_failure",
                "Simulated Keycloak role failure.");
        }

        return Task.CompletedTask;
    }

    public Task DeleteUserAsync(string userId, CancellationToken cancellationToken)
    {
        DeletedUserIds.Add(userId);
        Users.RemoveAll(user => user.Id == userId);

        return Task.CompletedTask;
    }

    public Task<OperatorUser?> GetUserByIdAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(user => user.Id == userId));

    public Task<OperatorUser> SetUserEnabledAsync(string userId, bool enabled, CancellationToken cancellationToken)
    {
        var user = Users.First(user => user.Id == userId);
        var updatedUser = user with
        {
            IsActive = enabled
        };

        Users.Remove(user);
        Users.Add(updatedUser);

        return Task.FromResult(updatedUser);
    }
}
