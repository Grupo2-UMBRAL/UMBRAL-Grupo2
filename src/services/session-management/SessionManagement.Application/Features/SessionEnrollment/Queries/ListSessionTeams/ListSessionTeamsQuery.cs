using MediatR;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed record ListSessionTeamsQuery(string JoinCode) : IRequest<SessionTeamsResponse>;
