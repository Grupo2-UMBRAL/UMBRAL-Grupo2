using MediatR;
using Umbral.ServiceDefaults;
using UserManagement.Domain.Entities;

namespace UserManagement.Application.Features.Operators.Commands.DeactivateOperator;

public sealed record DeactivateOperatorCommand(string UserId) : IRequest<OperatorUser>
{
}
