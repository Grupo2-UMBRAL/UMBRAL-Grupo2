using MediatR;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Operators.Commands.DeactivateOperator;

/// <summary>
/// Disables an Operator in Keycloak so it can no longer sign in, while keeping the account for
/// auditing — this is not a delete. Idempotent: deactivating an already-inactive Operator succeeds
/// and returns it unchanged rather than failing.
/// </summary>
/// <param name="UserId" example="7c9e6679-7425-40de-944b-e07fc1f90ae7">Keycloak user id of the Operator to disable, as returned by the list endpoint. Must resolve to an Operator (404 otherwise); ids of other realm users are not accepted.</param>
public sealed record DeactivateOperatorCommand(string UserId) : IRequest<OperatorDto>
{
}
