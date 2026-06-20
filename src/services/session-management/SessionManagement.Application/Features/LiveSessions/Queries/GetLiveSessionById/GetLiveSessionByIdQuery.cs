using MediatR;

namespace SessionManagement.Application.Features.LiveSessions;

public sealed record GetLiveSessionByIdQuery(Guid LiveSessionId) : IRequest<LiveSessionResponse>
{
}

