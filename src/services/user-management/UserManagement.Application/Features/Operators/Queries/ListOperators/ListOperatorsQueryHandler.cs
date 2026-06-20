using UserManagement.Application.Abstractions;
using MediatR;
using UserManagement.Domain.Entities;

namespace UserManagement.Application.Features.Operators.Queries.ListOperators;

public sealed class ListOperatorsQueryHandler(IOperatorAdministrationPort port)
    : IRequestHandler<ListOperatorsQuery, IReadOnlyList<OperatorUser>>
{
    public async Task<IReadOnlyList<OperatorUser>> Handle(ListOperatorsQuery request, CancellationToken cancellationToken)
    {
        var operators = await port.ListOperatorsAsync(cancellationToken);

        return operators
            .OrderByDescending(static user => user.IsActive)
            .ThenBy(static user => user.Username, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

