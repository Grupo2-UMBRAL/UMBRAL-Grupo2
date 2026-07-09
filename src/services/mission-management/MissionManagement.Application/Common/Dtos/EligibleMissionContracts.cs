namespace MissionManagement.Application.Common.Dtos;

// ---------------------------------------------------------------------------
// Eligible-for-live-session contracts (server-to-server; consumed by session-management)
// ---------------------------------------------------------------------------

public sealed record EligibleMissionForLiveSessionSummaryResponse(
    Guid Id,
    string Name,
    int MaximumDurationMinutes,
    string? GameType,
    int ActivePlayCount);

public sealed record EligibleMissionForLiveSessionResponse(
    Guid Id,
    string Name,
    string Description,
    int MaximumDurationMinutes,
    IReadOnlyList<EligiblePlayResponse> Plays);

public sealed record EligiblePlayResponse(
    Guid Id,
    int Order,
    string GameType,
    string Difficulty,
    int TimeLimitMinutes,
    string Prompt,
    IReadOnlyList<EligibleChoiceResponse>? Choices,
    Guid? CorrectChoiceId,
    string? ExpectedQrHash,
    IReadOnlyList<EligibleHintResponse> Hints);

// Player-facing choice: NEVER carries the correct flag.
public sealed record EligibleChoiceResponse(Guid Id, string Text);

public sealed record EligibleHintResponse(string Content, bool IsSolution, double? Latitude, double? Longitude);
