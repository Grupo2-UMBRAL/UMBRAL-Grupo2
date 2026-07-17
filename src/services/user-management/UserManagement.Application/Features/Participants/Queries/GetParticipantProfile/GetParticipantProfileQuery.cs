using MediatR;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Participants.Queries.GetParticipantProfile;

/// <summary>
/// Reads the calling Participant's own account. Carries no parameters on purpose: the account is the
/// one in the token, so there is nothing to authorize beyond being a Participant.
/// </summary>
public sealed record GetParticipantProfileQuery : IRequest<ParticipantProfileDto>;
