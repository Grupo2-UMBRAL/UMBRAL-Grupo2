using MediatR;
using UserManagement.Domain.Entities;

namespace UserManagement.Application.Features.Participants.Commands.CreateParticipant;

/// <summary>
/// Public participant self-registration. Mirrors <c>CreateOperatorCommand</c> but is issued
/// anonymously from the mobile client, collects only username/email/password, and assigns the
/// Participant realm role instead of Operator.
/// </summary>
public sealed record CreateParticipantCommand(
    string? Username,
    string? Email,
    string? Password) : IRequest<OperatorUser>
{
}
