using MediatR;
using UserManagement.Application.Abstractions;
using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Features.Participants.Queries.GetParticipantProfile;

public sealed class GetParticipantProfileQueryHandler(
    ICurrentParticipantIdentity currentParticipantIdentity,
    IParticipantAdministrationPort port)
    : IRequestHandler<GetParticipantProfileQuery, ParticipantProfileDto>
{
    public Task<ParticipantProfileDto> Handle(
        GetParticipantProfileQuery request,
        CancellationToken cancellationToken) =>
        port.GetParticipantProfileAsync(
            currentParticipantIdentity.GetRequiredParticipantUserId(),
            cancellationToken);
}
