using MediatR;

namespace SessionManagement.Application.Features.LiveSessions;

public sealed record ListLiveSessionsQuery : IRequest<IReadOnlyList<LiveSessionResponse>>
{
}
