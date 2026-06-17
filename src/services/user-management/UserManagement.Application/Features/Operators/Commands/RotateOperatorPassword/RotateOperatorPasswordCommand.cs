using MediatR;
using Umbral.ServiceDefaults;
using UserManagement.Domain.Entities;

namespace UserManagement.Application.Features.Operators.Commands.RotateOperatorPassword;

public sealed record RotateOperatorPasswordCommand(string UserId, string? Password) : IRequest<OperatorUser>
{
}
