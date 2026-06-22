using MediatR;

namespace SessionManagement.Application.Features.LiveSessions;

public sealed record CreateLiveSessionCommand(
    Guid MissionId,
    string Name,
    DateTimeOffset? ScheduledStartAtUtc,
    IReadOnlyList<Guid>? SelectedMissionStageIds) : IRequest<LiveSessionResponse>
{
}

