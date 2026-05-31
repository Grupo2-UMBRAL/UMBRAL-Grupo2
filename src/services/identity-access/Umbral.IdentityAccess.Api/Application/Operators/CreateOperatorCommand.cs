using MediatR;

namespace Umbral.IdentityAccess.Api.Application.Operators;

public sealed record CreateOperatorCommand(
    string? Username,
    string? Email,
    string? FirstName,
    string? LastName,
    string? Password) : IRequest<OperatorUser>;

public sealed class CreateOperatorCommandHandler(OperatorAdministrationService service)
    : IRequestHandler<CreateOperatorCommand, OperatorUser>
{
    public Task<OperatorUser> Handle(CreateOperatorCommand request, CancellationToken cancellationToken) =>
        service.CreateOperatorAsync(
            new CreateOperatorInput(
                request.Username,
                request.Email,
                request.FirstName,
                request.LastName,
                request.Password),
            cancellationToken);
}
