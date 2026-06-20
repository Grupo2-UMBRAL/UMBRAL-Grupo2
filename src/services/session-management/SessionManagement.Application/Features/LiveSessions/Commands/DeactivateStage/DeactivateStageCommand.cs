using MediatR;

namespace SessionManagement.Application.Features.LiveSessions;

public sealed record DeactivateStageCommand(Guid LiveSessionId, Guid MissionStageId) : IRequest<LiveSessionResponse>;

