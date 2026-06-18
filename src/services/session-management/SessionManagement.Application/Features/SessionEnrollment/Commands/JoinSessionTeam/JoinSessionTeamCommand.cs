using MediatR;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed record JoinSessionTeamCommand(string JoinCode, Guid SessionTeamId) : IRequest<JoinSessionTeamResponse>;
