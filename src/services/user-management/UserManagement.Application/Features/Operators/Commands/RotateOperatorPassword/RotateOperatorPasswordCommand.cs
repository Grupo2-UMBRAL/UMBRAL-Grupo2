using MediatR;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Operators.Commands.RotateOperatorPassword;

/// <summary>
/// Replaces an Operator's password with an explicitly supplied one. There is no "current password"
/// challenge — this is an Administrator resetting someone else's credential, not a self-service
/// change. The new password is emailed to the Operator; a failure to send does not roll the change
/// back. The returned <see cref="OperatorDto"/> is the pre-rotation snapshot: only the password
/// changed, so no other field is affected.
/// </summary>
/// <param name="UserId" example="7c9e6679-7425-40de-944b-e07fc1f90ae7">Keycloak user id of the Operator to reset, as returned by the list endpoint. Must resolve to an Operator (404 otherwise); ids of other realm users are not accepted.</param>
/// <param name="Password" example="replace-with-a-real-secret">The new password, 8 to 128 chars after trimming. Nullable only because the wire type allows a missing field: null or blank is rejected with <c>operator_password_required</c> (400) and does <b>not</b> mean "generate one" or "leave the password unchanged".</param>
public sealed record RotateOperatorPasswordCommand(string UserId, string? Password) : IRequest<OperatorDto>
{
}
