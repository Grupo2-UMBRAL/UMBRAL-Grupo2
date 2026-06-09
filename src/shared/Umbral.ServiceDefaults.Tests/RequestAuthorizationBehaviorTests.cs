using System.Security.Claims;
using MediatR;
using Xunit;

namespace Umbral.ServiceDefaults.Tests;

public sealed class RequestAuthorizationBehaviorTests
{
    [Fact]
    public async Task Handle_RejectsUnauthenticatedUserBeforeExecutingHandler()
    {
        var behavior = new RequestAuthorizationBehavior<AdministratorOnlyRequest, string>(
            new StubCurrentUserAccessor(new CurrentUser(new ClaimsPrincipal(new ClaimsIdentity()))),
            []);
        var nextWasCalled = false;

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            behavior.Handle(
                new AdministratorOnlyRequest(),
                () =>
                {
                    nextWasCalled = true;
                    return Task.FromResult("ok");
                },
                CancellationToken.None));

        Assert.Equal("authorization_required", exception.Code);
        Assert.Equal(UmbralFailureCategory.Unauthorized, exception.Category);
        Assert.False(nextWasCalled);
    }

    [Fact]
    public async Task Handle_RejectsUserWithoutRequiredRoleBeforeExecutingHandler()
    {
        var behavior = new RequestAuthorizationBehavior<AdministratorOnlyRequest, string>(
            new StubCurrentUserAccessor(CreateCurrentUser(UmbralRoles.Operator)),
            []);
        var nextWasCalled = false;

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            behavior.Handle(
                new AdministratorOnlyRequest(),
                () =>
                {
                    nextWasCalled = true;
                    return Task.FromResult("ok");
                },
                CancellationToken.None));

        Assert.Equal("authorization_role_forbidden", exception.Code);
        Assert.Equal(UmbralFailureCategory.Forbidden, exception.Category);
        Assert.False(nextWasCalled);
    }

    [Fact]
    public async Task Handle_RejectsUserWithoutScopeBeforeExecutingHandler()
    {
        var behavior = new RequestAuthorizationBehavior<ScopedParticipantRequest, string>(
            new StubCurrentUserAccessor(CreateCurrentUser(UmbralRoles.Participant)),
            [new DenyingScopeValidator()]);
        var nextWasCalled = false;

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            behavior.Handle(
                new ScopedParticipantRequest("team-b"),
                () =>
                {
                    nextWasCalled = true;
                    return Task.FromResult("ok");
                },
                CancellationToken.None));

        Assert.Equal("authorization_scope_forbidden", exception.Code);
        Assert.Equal(UmbralFailureCategory.Forbidden, exception.Category);
        Assert.False(nextWasCalled);
    }

    [Fact]
    public async Task Handle_AllowsUserWhenRoleAndScopePass()
    {
        var behavior = new RequestAuthorizationBehavior<ScopedParticipantRequest, string>(
            new StubCurrentUserAccessor(CreateCurrentUser(UmbralRoles.Participant)),
            [new AllowingScopeValidator()]);

        var response = await behavior.Handle(
            new ScopedParticipantRequest("team-a"),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        Assert.Equal("ok", response);
    }

    private static CurrentUser CreateCurrentUser(string role)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role), new Claim("sub", "user-1")],
            authenticationType: "Test");

        return new CurrentUser(new ClaimsPrincipal(identity));
    }

    private sealed record AdministratorOnlyRequest : IRequest<string>, IAuthorizableRequest
    {
        public RequestAuthorizationMetadata Authorization => UmbralRequestAuthorizations.AdministratorOnly;
    }

    private sealed record ScopedParticipantRequest(string SessionTeamId) : IRequest<string>, IAuthorizableRequest
    {
        public RequestAuthorizationMetadata Authorization => UmbralRequestAuthorizations.ParticipantOnly;
    }

    private sealed class StubCurrentUserAccessor(CurrentUser currentUser) : ICurrentUserAccessor
    {
        public CurrentUser GetCurrentUser() => currentUser;
    }

    private sealed class DenyingScopeValidator : IRequestScopeValidator<ScopedParticipantRequest>
    {
        public Task<bool> HasAccessToScopeAsync(
            ScopedParticipantRequest request,
            CurrentUser currentUser,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class AllowingScopeValidator : IRequestScopeValidator<ScopedParticipantRequest>
    {
        public Task<bool> HasAccessToScopeAsync(
            ScopedParticipantRequest request,
            CurrentUser currentUser,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }
}
