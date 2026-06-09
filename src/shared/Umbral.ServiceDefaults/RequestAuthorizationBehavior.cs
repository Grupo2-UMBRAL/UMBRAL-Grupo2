using MediatR;

namespace Umbral.ServiceDefaults;

public sealed class RequestAuthorizationBehavior<TRequest, TResponse>(
    ICurrentUserAccessor currentUserAccessor,
    IEnumerable<IRequestScopeValidator<TRequest>> scopeValidators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        if (request is not IAuthorizableRequest authorizableRequest)
        {
            return await next();
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        if (!currentUser.IsAuthenticated)
        {
            throw new UmbralDomainException(
                "authorization_required",
                "Authentication is required for this operation.",
                UmbralFailureCategory.Unauthorized);
        }

        if (authorizableRequest.Authorization.AllowedRoles.Count > 0
            && !authorizableRequest.Authorization.AllowedRoles.Any(currentUser.IsInRole))
        {
            throw new UmbralDomainException(
                "authorization_role_forbidden",
                "Current user is not allowed to perform this operation.",
                UmbralFailureCategory.Forbidden);
        }

        foreach (var scopeValidator in scopeValidators)
        {
            var hasAccessToScope = await scopeValidator.HasAccessToScopeAsync(
                request,
                currentUser,
                cancellationToken);
            if (!hasAccessToScope)
            {
                throw new UmbralDomainException(
                    "authorization_scope_forbidden",
                    "Current user is not allowed to access the requested scope.",
                    UmbralFailureCategory.Forbidden);
            }
        }

        return await next();
    }
}
