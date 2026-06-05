namespace Umbral.ServiceDefaults;

public interface IRequestScopeValidator<in TRequest>
{
    Task<bool> HasAccessToScopeAsync(
        TRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken);
}
