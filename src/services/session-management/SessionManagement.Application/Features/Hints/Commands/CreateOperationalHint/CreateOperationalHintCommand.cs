using MediatR;
using SessionManagement.Application.Features.LiveSessions;

namespace SessionManagement.Application.Features.Hints;

public sealed record CreateOperationalHintCommand(
    Guid LiveSessionId,
    Guid MissionStageId,
    string Content,
    double? Latitude,
    double? Longitude) : IRequest<LiveSessionStageHintResponse>;

/// <summary>
/// A Hint an operator improvises during a LiveSession for a Play that is still pending. It belongs
/// to this execution's Session Flow only and does not edit the reusable Mission.
/// </summary>
/// <param name="Content" example="Look behind the fountain">Text the Session Team will read once the Hint is released. Creating it does not release it.</param>
/// <param name="Latitude" example="-34.603722">Latitude of the place the Hint points to. null = a text-only Hint with no map location; pair it with Longitude or omit both.</param>
/// <param name="Longitude" example="-58.381592">Longitude of the place the Hint points to. null = a text-only Hint with no map location; pair it with Latitude or omit both.</param>
public sealed record CreateOperationalHintRequest(string Content, double? Latitude, double? Longitude);

