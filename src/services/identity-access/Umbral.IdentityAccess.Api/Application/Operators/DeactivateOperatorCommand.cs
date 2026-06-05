using Umbral.ServiceDefaults;
using MediatR;

namespace Umbral.IdentityAccess.Api.Application.Operators;

public sealed record DeactivateOperatorCommand(string UserId) : IRequest<OperatorUser>, IAuthorizableRequest
{
    public RequestAuthorizationMetadata Authorization => UmbralRequestAuthorizations.AdministratorOnly;
}

public sealed class DeactivateOperatorCommandHandler(OperatorAdministrationService service)
    : IRequestHandler<DeactivateOperatorCommand, OperatorUser>
{
    public Task<OperatorUser> Handle(
        DeactivateOperatorCommand request,
        CancellationToken cancellationToken) =>
        service.DeactivateOperatorAsync(request.UserId, cancellationToken);
}
