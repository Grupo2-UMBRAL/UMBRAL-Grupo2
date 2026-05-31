using MediatR;

namespace Umbral.IdentityAccess.Api.Application.Operators;

public sealed record RotateOperatorPasswordCommand(string UserId, string? Password) : IRequest<OperatorUser>;

public sealed class RotateOperatorPasswordCommandHandler(OperatorAdministrationService service)
    : IRequestHandler<RotateOperatorPasswordCommand, OperatorUser>
{
    public Task<OperatorUser> Handle(
        RotateOperatorPasswordCommand request,
        CancellationToken cancellationToken) =>
        service.RotateOperatorPasswordAsync(request.UserId, request.Password, cancellationToken);
}
