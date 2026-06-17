using MediatR;
using Umbral.ServiceDefaults;
using UserManagement.Domain.Entities;

namespace UserManagement.Application.Features.Operators.Queries.ListOperators;

public sealed record ListOperatorsQuery : IRequest<IReadOnlyList<OperatorUser>>
{
}
