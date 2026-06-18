using MediatR;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed record RegisterTeamCommand(string JoinCode, string TeamName) : IRequest<RegisterTeamResponse>;
