using Umbral.MissionDesign.Api.Domain.Missions;

namespace Umbral.MissionDesign.Api.Application.Missions;

public sealed record MissionResponse(
    Guid Id,
    string Name,
    string Description,
    string Difficulty,
    int MaximumDurationMinutes,
    string GameType,
    bool IsActive,
    IReadOnlyList<MissionNodeResponse> Nodes);

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
    string GameType,
    IReadOnlyList<MissionNodeRequest>? Nodes = null);

public sealed record UpdateMissionRequest(
    string Name,
    string Description,
    string Difficulty,
    int MaximumDurationMinutes,
    string GameType,
    IReadOnlyList<MissionNodeRequest>? Nodes = null);

public sealed record MissionNodeRequest(
    Guid? Id = null,
    string Name = "",
    int Order = 0,
    bool IsActive = true,
    int? DefaultTimeBudgetMinutes = null,
    int? TimeBudgetMinutes = null,
    string? GameType = null,
    string? ExpectedQrHash = null,
    string? TriviaValidAnswer = null,
    string? TriviaInitialValidationCriterion = null,
    IReadOnlyList<MissionHintRequest>? Hints = null,
    IReadOnlyList<MissionNodeRequest>? Children = null);

public sealed record MissionHintRequest(
    Guid? Id = null,
    string Content = "",
    bool IsSolution = false,
    decimal? Latitude = null,
    decimal? Longitude = null);

public sealed record MissionNodeResponse(
    Guid Id,
    string Name,
    int Order,
    bool IsActive,
    int? DefaultTimeBudgetMinutes,
    int? TimeBudgetMinutes,
    int? ResolvedTimeBudgetMinutes,
    string? GameType,
    string? ExpectedQrHash,
    string? TriviaValidAnswer,
    string? TriviaInitialValidationCriterion,
    bool IsLeaf,
    IReadOnlyList<MissionHintResponse> Hints,
    IReadOnlyList<MissionNodeResponse> Children);

public sealed record MissionHintResponse(
    Guid Id,
    string Content,
    bool IsSolution,
    decimal? Latitude,
    decimal? Longitude);

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
            mission.IsActive,
            mission.Nodes.Select(node => node.ToResponse(mission.MaximumDurationMinutes)).ToArray());
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

    public static MissionNode ToDomain(this MissionNodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return MissionNode.Create(
            request.Id ?? Guid.NewGuid(),
            request.Name,
            request.Order,
            request.IsActive,
            request.DefaultTimeBudgetMinutes,
            request.TimeBudgetMinutes,
            request.GameType,
            request.ExpectedQrHash,
            request.TriviaValidAnswer,
            request.TriviaInitialValidationCriterion,
            request.Hints?.Select(hint => hint.ToDomain()).ToArray(),
            request.Children?.Select(child => child.ToDomain()).ToArray());
    }

    public static MissionHint ToDomain(this MissionHintRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return MissionHint.Create(
            request.Id ?? Guid.NewGuid(),
            request.Content,
            request.IsSolution,
            request.Latitude,
            request.Longitude);
    }

    private static MissionNodeResponse ToResponse(this MissionNode node, int inheritedTimeBudgetMinutes)
    {
        var resolvedTimeBudgetMinutes = node.ResolveTimeBudgetMinutes(inheritedTimeBudgetMinutes);

        return new MissionNodeResponse(
            node.Id,
            node.Name,
            node.Order,
            node.IsActive,
            node.DefaultTimeBudgetMinutes,
            node.TimeBudgetMinutes,
            resolvedTimeBudgetMinutes,
            node.GameType,
            node.ExpectedQrHash,
            node.TriviaValidAnswer,
            node.TriviaInitialValidationCriterion,
            node.IsLeaf,
            node.Hints.Select(hint => hint.ToResponse()).ToArray(),
            node.Children.Select(child => child.ToResponse(resolvedTimeBudgetMinutes)).ToArray());
    }

    private static MissionHintResponse ToResponse(this MissionHint hint)
    {
        return new MissionHintResponse(
            hint.Id,
            hint.Content,
            hint.IsSolution,
            hint.Latitude,
            hint.Longitude);
    }
}
