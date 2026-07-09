using MediatR;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Operators.Commands.DeactivateOperator;

public sealed record DeactivateOperatorCommand(string UserId) : IRequest<OperatorDto>
{
}
