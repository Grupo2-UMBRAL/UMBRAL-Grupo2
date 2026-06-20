using MediatR;
using SessionManagement.Application.Features.LiveSessions;

namespace SessionManagement.Application.Features.Hints;

public sealed record CreateOperationalHintCommand(
    Guid LiveSessionId,
    Guid MissionStageId,
    string Content,
    double? Latitude,
    double? Longitude) : IRequest<LiveSessionStageHintResponse>;

public sealed record CreateOperationalHintRequest(string Content, double? Latitude, double? Longitude);

