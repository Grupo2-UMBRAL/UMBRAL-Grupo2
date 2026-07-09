using MediatR;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Operators.Commands.CreateOperator;

public sealed record CreateOperatorCommand(
    string? Username,
    string? Email,
    string? FirstName,
    string? LastName,
    string? Password) : IRequest<OperatorDto>
{
}

