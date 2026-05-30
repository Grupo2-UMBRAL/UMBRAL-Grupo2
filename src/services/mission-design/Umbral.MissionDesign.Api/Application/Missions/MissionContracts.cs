using Umbral.MissionDesign.Api.Domain.Missions;

namespace Umbral.MissionDesign.Api.Application.Missions;

public sealed record MissionResponse(
    Guid Id,
    string Name,
    string Description,
    string Difficulty,
    int MaximumDurationMinutes,
    string GameType,
    bool IsActive);

public sealed record MissionSummaryResponse(
    Guid Id,
    string Name,
    string Difficulty,
    int MaximumDurationMinutes,
    string GameType,
    bool IsActive);

public sealed record CreateMissionRequest(
    string Name,
    string Description,
    string Difficulty,
    int MaximumDurationMinutes,
    string GameType);

public sealed record UpdateMissionRequest(
    string Name,
    string Description,
    string Difficulty,
    int MaximumDurationMinutes);

public static class MissionMappings
{
    public static MissionResponse ToResponse(this Mission mission)
    {
        ArgumentNullException.ThrowIfNull(mission);

        return new MissionResponse(
            mission.Id,
            mission.Name,
            mission.Description,
            mission.Difficulty,
            mission.MaximumDurationMinutes,
            mission.GameType,
            mission.IsActive);
    }

    public static MissionSummaryResponse ToSummaryResponse(this Mission mission)
    {
        ArgumentNullException.ThrowIfNull(mission);

        return new MissionSummaryResponse(
            mission.Id,
            mission.Name,
            mission.Difficulty,
            mission.MaximumDurationMinutes,
            mission.GameType,
            mission.IsActive);
    }
}
