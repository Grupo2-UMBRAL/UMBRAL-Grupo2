namespace MissionManagement.Application.Common.Dtos;

// ---------------------------------------------------------------------------
// Write contracts (admin CRUD)
// ---------------------------------------------------------------------------

public sealed record CreateMissionRequest(
    string Name,
    string Description,
    int MaximumDurationMinutes,
    IReadOnlyList<MissionItemRequest>? Items = null);

public sealed record UpdateMissionRequest(
    string Name,
    string Description,
    int MaximumDurationMinutes,
    IReadOnlyList<MissionItemRequest>? Items = null);

public sealed record MissionItemRequest(
    string Kind = MissionItemKind.Section,
    Guid? Id = null,
    int Order = 0,
    string? Title = null,
    // Section
    IReadOnlyList<MissionItemRequest>? Children = null,
    // Challenge
    string? GameType = null,
    string? Difficulty = null,
    int? TimeLimitMinutes = null,
    bool IsActive = true,
    IReadOnlyList<QuestionRequest>? Questions = null,
    IReadOnlyList<SearchRequest>? Searches = null);

public sealed record QuestionRequest(
    Guid? Id,
    int Order,
    string Text,
    string? DifficultyOverride,
    int? TimeLimitMinutesOverride,
    IReadOnlyList<ChoiceRequest> Choices);

public sealed record ChoiceRequest(
    Guid? Id,
    int Order,
    string Text,
    bool IsCorrect);

public sealed record SearchRequest(
    Guid? Id,
    int Order,
    string Clue,
    string ExpectedQrHash,
    string? DifficultyOverride,
    int? TimeLimitMinutesOverride,
    IReadOnlyList<HintRequest>? Hints);

public sealed record HintRequest(
    Guid? Id,
    int Order,
    string Content,
    bool IsSolution,
    double? Latitude,
    double? Longitude);
