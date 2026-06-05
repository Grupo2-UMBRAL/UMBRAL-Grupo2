using Umbral.ServiceDefaults;
using MediatR;

namespace Umbral.IdentityAccess.Api.Application.Operators;

public sealed record RotateOperatorPasswordCommand(string UserId, string? Password) : IRequest<OperatorUser>, IAuthorizableRequest
{
    public RequestAuthorizationMetadata Authorization => UmbralRequestAuthorizations.AdministratorOnly;
}

public sealed class RotateOperatorPasswordCommandHandler(OperatorAdministrationService service)
    : IRequestHandler<RotateOperatorPasswordCommand, OperatorUser>
{
    public Task<OperatorUser> Handle(
        RotateOperatorPasswordCommand request,
        CancellationToken cancellationToken) =>
        service.RotateOperatorPasswordAsync(request.UserId, request.Password, cancellationToken);
}
