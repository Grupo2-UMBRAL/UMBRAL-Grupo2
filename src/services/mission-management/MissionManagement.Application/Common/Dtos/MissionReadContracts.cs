namespace MissionManagement.Application.Common.Dtos;

// ---------------------------------------------------------------------------
// Read contracts (admin authors and sees correct answers — ADR §9)
// ---------------------------------------------------------------------------

public sealed record MissionResponse(
    Guid Id,
    string Name,
    string Description,
    int MaximumDurationMinutes,
    bool IsActive,
    IReadOnlyList<MissionItemResponse> Items);

public sealed record MissionSummaryResponse(
    Guid Id,
    string Name,
    int MaximumDurationMinutes,
    bool IsActive);

public sealed record MissionItemResponse(
    string Kind,
    Guid Id,
    int Order,
    string? Title,
    // Section
    IReadOnlyList<MissionItemResponse>? Children,
    // Challenge
    string? GameType,
    string? Difficulty,
    int? TimeLimitMinutes,
    bool? IsActive,
    IReadOnlyList<QuestionResponse>? Questions,
    IReadOnlyList<SearchResponse>? Searches);

public sealed record QuestionResponse(
    Guid Id,
    int Order,
    string Text,
    string? DifficultyOverride,
    int? TimeLimitMinutesOverride,
    IReadOnlyList<ChoiceResponse> Choices);

public sealed record ChoiceResponse(
    Guid Id,
    int Order,
    string Text,
    bool IsCorrect);

public sealed record SearchResponse(
    Guid Id,
    int Order,
    string Clue,
    string ExpectedQrHash,
    string? DifficultyOverride,
    int? TimeLimitMinutesOverride,
    IReadOnlyList<HintResponse> Hints);

public sealed record HintResponse(
    Guid Id,
    int Order,
    string Content,
    bool IsSolution,
    double? Latitude,
    double? Longitude);
