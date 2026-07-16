using MediatR;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Participants.Commands.CreateParticipant;

/// <summary>
/// Public participant self-registration. Mirrors <c>CreateOperatorCommand</c> but is issued
/// anonymously from the mobile client, collects only username/email/password, and assigns the
/// Participant realm role instead of Operator.
/// </summary>
/// <param name="Username" example="pao.rojas">Login handle and the name shown to other players; must be unique in the realm (409 otherwise). 3 to 40 chars, letters, digits, dot, underscore or dash only. Also reused as the Participant's first name in Keycloak, since self-registration does not collect one.</param>
/// <param name="Email" example="pao.rojas@example.cl">Must be unique in the realm (409 otherwise), and is where the welcome email is sent. Max 120 chars; stored lower-cased. Not echoed back in the response.</param>
/// <param name="Password" example="replace-with-a-real-secret">Chosen by the Participant. 8 to 128 chars after trimming; no complexity rule is enforced here. Unlike the Operator flow, it is never emailed back.</param>
public sealed record CreateParticipantCommand(
    string? Username,
    string? Email,
    string? Password) : IRequest<ParticipantDto>
{
}
