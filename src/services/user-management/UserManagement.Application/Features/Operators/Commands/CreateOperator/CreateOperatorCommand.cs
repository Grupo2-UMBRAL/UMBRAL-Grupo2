using MediatR;
using Umbral.ServiceDefaults;
using UserManagement.Domain.Entities;

namespace UserManagement.Application.Features.Operators.Commands.CreateOperator;

public sealed record CreateOperatorCommand(
    string? Username,
    string? Email,
    string? FirstName,
    string? LastName,
    string? Password) : IRequest<OperatorUser>
{
}

