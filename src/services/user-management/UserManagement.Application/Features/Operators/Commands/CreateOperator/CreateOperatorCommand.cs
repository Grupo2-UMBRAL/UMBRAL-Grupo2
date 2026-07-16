using MediatR;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Operators.Commands.CreateOperator;

/// <summary>
/// Data required to provision an Operator in Keycloak and grant it the Operator realm role.
/// Every field is required despite being nullable: the nullability only lets malformed JSON reach
/// the validator, which rejects blanks with an <c>operator_*_required</c> code. On success the
/// credentials are emailed to <paramref name="Email"/>.
/// </summary>
/// <param name="Username" example="a.silva">Login handle; must be unique in the realm (409 otherwise). Max 40 chars, letters, digits, dot, underscore or dash only.</param>
/// <param name="Email" example="a.silva@umbral.cl">Also must be unique in the realm (409 otherwise), and is where the credentials email is sent. Max 120 chars; stored lower-cased.</param>
/// <param name="FirstName" example="Ana">Display name for the admin console only; never used to sign in. Max 80 chars.</param>
/// <param name="LastName" example="Silva">Display name for the admin console only; never used to sign in. Max 80 chars.</param>
/// <param name="Password" example="replace-with-a-real-secret">Initial password, set directly rather than generated. 8 to 128 chars after trimming; no complexity rule is enforced here. Sent to the Operator by email, so treat it as a first-login secret.</param>
public sealed record CreateOperatorCommand(
    string? Username,
    string? Email,
    string? FirstName,
    string? LastName,
    string? Password) : IRequest<OperatorDto>
{
}

