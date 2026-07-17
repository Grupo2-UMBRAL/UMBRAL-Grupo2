using MediatR;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Operators.Commands.CreateOperator;

/// <summary>
/// Data required to provision an Operator in Keycloak and grant it the Operator realm role. Both
/// fields are required despite being nullable: the nullability only lets malformed JSON reach the
/// validator, which rejects blanks with an <c>operator_*_required</c> code. The Administrator no
/// longer sets a password or names — the account is created without a credential, and Keycloak emails
/// the Operator a one-time link to set their own password and complete their profile.
/// </summary>
/// <param name="Username" example="a.silva">Login handle; must be unique in the realm (409 otherwise). Max 40 chars, letters, digits, dot, underscore or dash only.</param>
/// <param name="Email" example="a.silva@umbral.cl">Also must be unique in the realm (409 otherwise), and is where the onboarding invitation is sent. Max 120 chars; stored lower-cased.</param>
public sealed record CreateOperatorCommand(
    string? Username,
    string? Email) : IRequest<OperatorDto>
{
}
