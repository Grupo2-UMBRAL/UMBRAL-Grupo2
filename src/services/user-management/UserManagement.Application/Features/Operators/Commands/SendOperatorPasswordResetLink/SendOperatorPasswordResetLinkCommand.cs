using MediatR;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Operators.Commands.SendOperatorPasswordResetLink;

/// <summary>Sends an Operator a Keycloak link to choose a new password.</summary>
public sealed record SendOperatorPasswordResetLinkCommand(string UserId) : IRequest<OperatorDto>;
