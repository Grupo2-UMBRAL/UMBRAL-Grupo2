using MediatR;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Operators.Commands.ResendOperatorInvitation;

/// <summary>
/// Re-issues the Keycloak onboarding invitation for an existing Operator: a fresh one-time link to set
/// a password, complete the profile, and verify the email. Used when the first invitation was lost,
/// expired, or its dispatch failed — no new account is created and no password is set. Returns the
/// Operator unchanged; the effect is the email Keycloak sends, not a state change in UMBRAL.
/// </summary>
/// <param name="UserId" example="7c9e6679-7425-40de-944b-e07fc1f90ae7">Keycloak user id of the Operator to re-invite, as returned by the list endpoint. Must resolve to an Operator (404 otherwise).</param>
public sealed record ResendOperatorInvitationCommand(string UserId) : IRequest<OperatorDto>
{
}
