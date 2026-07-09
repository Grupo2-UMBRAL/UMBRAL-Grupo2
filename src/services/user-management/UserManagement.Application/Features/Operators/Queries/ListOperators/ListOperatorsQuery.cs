using MediatR;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Operators.Queries.ListOperators;

public sealed record ListOperatorsQuery : IRequest<IReadOnlyList<OperatorDto>>
{
}
