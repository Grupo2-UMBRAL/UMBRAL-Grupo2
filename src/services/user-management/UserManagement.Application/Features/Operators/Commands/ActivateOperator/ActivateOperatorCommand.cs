using MediatR;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Operators.Commands.ActivateOperator;

/// <summary>Re-enables a deactivated Operator in Keycloak.</summary>
public sealed record ActivateOperatorCommand(string UserId) : IRequest<OperatorDto>;
