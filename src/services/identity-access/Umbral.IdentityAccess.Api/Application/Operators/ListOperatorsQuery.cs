using MediatR;

namespace Umbral.IdentityAccess.Api.Application.Operators;

public sealed record ListOperatorsQuery() : IRequest<IReadOnlyList<OperatorUser>>;

public sealed class ListOperatorsQueryHandler(OperatorAdministrationService service)
    : IRequestHandler<ListOperatorsQuery, IReadOnlyList<OperatorUser>>
{
    public Task<IReadOnlyList<OperatorUser>> Handle(
        ListOperatorsQuery request,
        CancellationToken cancellationToken) =>
        service.ListOperatorsAsync(cancellationToken);
}
