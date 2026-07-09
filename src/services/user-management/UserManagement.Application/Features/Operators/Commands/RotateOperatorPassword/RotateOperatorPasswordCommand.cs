using MediatR;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Operators.Commands.RotateOperatorPassword;

public sealed record RotateOperatorPasswordCommand(string UserId, string? Password) : IRequest<OperatorDto>
{
}
