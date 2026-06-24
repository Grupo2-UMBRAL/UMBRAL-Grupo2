using SessionManagement.Domain.LiveSessions;
using SessionManagement.Application.Features.SessionLifecycle;

namespace SessionManagement.Application.Features.LiveSessions;

public sealed record CreateLiveSessionRequest(
    Guid MissionId,
    string Name,
    DateTimeOffset? ScheduledStartAtUtc,
    IReadOnlyList<Guid>? SelectedMissionStageIds);

public sealed record LiveSessionResponse(
    Guid Id,
    Guid MissionId,
    string MissionName,
    string Name,
    string State,
    DateTimeOffset? ScheduledStartAtUtc,
    DateTimeOffset CreatedAtUtc,
    string? JoinCode,
    DateTimeOffset? EnrollmentWindowOpenedAtUtc,
    DateTimeOffset? EnrollmentWindowClosedAtUtc,
    int RegisteredSessionTeamCount,
    IReadOnlyList<LiveSessionStageResponse> SessionStageFlow);

// Operator/administrator-facing projection of a Play in the Session Flow.
public sealed record LiveSessionStageResponse(
    Guid MissionStageId,
    string Name,
    int SessionStageOrder,
    int SourceOrder,
    int ResolvedTimeBudgetMinutes,
    string Difficulty,
    string GameType,
    string Prompt,
    string? ExpectedQrHash,
    IReadOnlyList<LiveSessionChoiceResponse> Choices,
    IReadOnlyList<LiveSessionStageHintResponse> Hints);

public sealed record LiveSessionChoiceResponse(
    Guid Id,
    string Text);

public sealed record LiveSessionStageHintResponse(
    Guid Id,
    string Content,
    bool IsSolution,
    decimal? Latitude,
    decimal? Longitude);

// Eligible Mission snapshot deserialized from
// GET api/mission-management/missions/eligible-for-live-session/{missionId}.
public sealed record EligibleMissionForLiveSessionSnapshot(
    Guid Id,
    string Name,
    string? Description,
    int MaximumDurationMinutes,
    IReadOnlyList<EligiblePlaySnapshot> Plays);

public sealed record EligiblePlaySnapshot(
    Guid Id,
    int Order,
    string GameType,
    string Difficulty,
    int TimeLimitMinutes,
    string Prompt,
    IReadOnlyList<EligibleChoiceSnapshot>? Choices,
    Guid? CorrectChoiceId,
    string? ExpectedQrHash,
    IReadOnlyList<EligiblePlayHintSnapshot>? Hints);

public sealed record EligibleChoiceSnapshot(
    Guid Id,
    string Text);

public sealed record EligiblePlayHintSnapshot(
    string Content,
    bool IsSolution,
    decimal? Latitude,
    decimal? Longitude);

public interface IMissionManagementLiveSessionCatalog
{
    Task<EligibleMissionForLiveSessionSnapshot> GetEligibleMissionForLiveSessionAsync(
        Guid missionId,
        CancellationToken cancellationToken);
}

public static class LiveSessionMappings
{
    public static LiveSessionResponse ToResponse(this LiveSession liveSession)
    {
        ArgumentNullException.ThrowIfNull(liveSession);

        return new LiveSessionResponse(
            liveSession.Id,
            liveSession.MissionId,
            liveSession.MissionName,
            liveSession.Name,
            liveSession.State,
            liveSession.ScheduledStartAtUtc,
            liveSession.CreatedAtUtc,
            liveSession.JoinCodeValue,
            liveSession.EnrollmentWindowOpenedAtUtc,
            liveSession.EnrollmentWindowClosedAtUtc,
            liveSession.SessionTeams.Count,
            liveSession.SessionStageFlow.Select(stage => stage.ToResponse()).ToArray());
    }

    public static LiveSessionStateResponse ToStateResponse(this LiveSession liveSession)
    {
        ArgumentNullException.ThrowIfNull(liveSession);

        return new LiveSessionStateResponse(
            liveSession.Id,
            liveSession.State,
            liveSession.SessionTeams.Count,
            liveSession.EnrollmentWindowOpenedAtUtc,
            liveSession.EnrollmentWindowClosedAtUtc);
    }

    // Maps an eligible Play onto the flat Session Flow item. The linear model
    // exposes a single global `order`; it is used as the Play's source order,
    // while the contiguous flow order is assigned by the Session Flow builder.
    public static LiveSessionStage ToDomain(this EligiblePlaySnapshot play, int sessionStageOrder)
    {
        ArgumentNullException.ThrowIfNull(play);

        return LiveSessionStage.Create(
            play.Id,
            $"Play {play.Order}",
            sessionStageOrder,
            play.Order,
            play.TimeLimitMinutes,
            play.Difficulty,
            play.GameType,
            play.Prompt,
            play.ExpectedQrHash,
            play.Choices?.Select(choice => choice.ToDomain()).ToArray(),
            play.CorrectChoiceId,
            play.Hints?.Select(hint => hint.ToDomain()).ToArray());
    }

    private static LiveSessionStageResponse ToResponse(this LiveSessionStage liveSessionStage)
    {
        return new LiveSessionStageResponse(
            liveSessionStage.MissionStageId,
            liveSessionStage.Name,
            liveSessionStage.SessionStageOrder,
            liveSessionStage.SourceOrder,
            liveSessionStage.ResolvedTimeBudgetMinutes,
            liveSessionStage.Difficulty,
            liveSessionStage.GameType,
            liveSessionStage.Prompt,
            liveSessionStage.ExpectedQrHash,
            liveSessionStage.Choices.Select(choice => new LiveSessionChoiceResponse(choice.Id, choice.Text)).ToArray(),
            liveSessionStage.Hints.Select(hint => hint.ToResponse()).ToArray());
    }

    private static LiveSessionChoice ToDomain(this EligibleChoiceSnapshot choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        return LiveSessionChoice.Create(choice.Id, choice.Text);
    }

    private static LiveSessionStageHint ToDomain(this EligiblePlayHintSnapshot hint)
    {
        ArgumentNullException.ThrowIfNull(hint);

        return LiveSessionStageHint.Create(
            Guid.NewGuid(),
            hint.Content,
            hint.IsSolution,
            hint.Latitude,
            hint.Longitude);
    }

    private static LiveSessionStageHintResponse ToResponse(this LiveSessionStageHint hint)
    {
        return new LiveSessionStageHintResponse(
            hint.Id,
            hint.Content,
            hint.IsSolution,
            hint.Latitude,
            hint.Longitude);
    }
}
