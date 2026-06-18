using MissionManagement.Domain.Missions;

namespace MissionManagement.Application.Features.MissionStages;

public sealed record MissionStageHintResponse(
    Guid Id,
    string Content,
    bool IsSolution,
    double? Latitude,
    double? Longitude);

public sealed record MissionStageResponse(
    Guid Id,
    Guid MissionId,
    string Name,
    int Order,
    string Difficulty,
    string GameType,
    string? ExpectedQrHash,
    string? TriviaValidationCriteria,
    bool IsActive,
    List<MissionStageHintResponse> Hints);

public sealed record MissionStageSummaryResponse(
    Guid Id,
    string Name,
    int Order,
    string Difficulty,
    string GameType,
    bool IsActive,
    int HintCount);

public sealed record CreateMissionStageRequest(
    string Name,
    int Order,
    string Difficulty,
    string GameType,
    string? ExpectedQrHash,
    string? TriviaValidationCriteria);

public sealed record CreateMissionStageHintRequest(
    string Content,
    bool IsSolution,
    double? Latitude,
    double? Longitude);

public static class MissionStageMappings
{
    public static MissionStageResponse ToResponse(this MissionStage missionStage)
    {
        ArgumentNullException.ThrowIfNull(missionStage);

        return new MissionStageResponse(
            missionStage.Id,
            missionStage.MissionId,
            missionStage.Name,
            missionStage.Order,
            missionStage.Difficulty,
            missionStage.GameType,
            missionStage.ExpectedQrHash,
            missionStage.TriviaValidationCriteria,
            missionStage.IsActive,
            missionStage.Hints
                .OrderBy(hint => hint.Id)
                .Select(hint => hint.ToResponse())
                .ToList());
    }

    public static MissionStageSummaryResponse ToSummaryResponse(this MissionStage missionStage)
    {
        ArgumentNullException.ThrowIfNull(missionStage);

        return new MissionStageSummaryResponse(
            missionStage.Id,
            missionStage.Name,
            missionStage.Order,
            missionStage.Difficulty,
            missionStage.GameType,
            missionStage.IsActive,
            missionStage.Hints.Count);
    }

    public static MissionStageHintResponse ToResponse(this MissionStageHint hint)
    {
        ArgumentNullException.ThrowIfNull(hint);

        return new MissionStageHintResponse(
            hint.Id,
            hint.Content,
            hint.IsSolution,
            hint.Latitude,
            hint.Longitude);
    }
}
