using SessionOperations.Domain.LiveSessions;
using SessionOperations.Application.Features.SessionLifecycle;

namespace SessionOperations.Application.Features.LiveSessions;

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
    string? TriviaValidAnswer,
    string? TriviaInitialValidationCriterion,
    IReadOnlyList<LiveSessionStageHintResponse> Hints);

public sealed record LiveSessionStageHintResponse(
    Guid Id,
    string Content,
    bool IsSolution,
    decimal? Latitude,
    decimal? Longitude);

public sealed record EligibleMissionForLiveSessionSnapshot(
    Guid Id,
    string Name,
    IReadOnlyList<EligibleMissionStageSnapshot> MissionStages);

public sealed record EligibleMissionStageSnapshot(
    Guid Id,
    string Name,
    int SessionStageOrder,
    int SourceOrder,
    int ResolvedTimeBudgetMinutes,
    string Difficulty,
    string GameType,
    string Prompt,
    string? ExpectedQrHash,
    string? TriviaValidAnswer,
    string? TriviaInitialValidationCriterion,
    IReadOnlyList<EligibleMissionStageHintSnapshot> Hints);

public sealed record EligibleMissionStageHintSnapshot(
    Guid Id,
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

    public static LiveSessionStage ToDomain(this EligibleMissionStageSnapshot missionStage, int sessionStageOrder)
    {
        ArgumentNullException.ThrowIfNull(missionStage);

        return LiveSessionStage.Create(
            missionStage.Id,
            missionStage.Name,
            sessionStageOrder,
            missionStage.SourceOrder,
            missionStage.ResolvedTimeBudgetMinutes,
            missionStage.Difficulty,
            missionStage.GameType,
            missionStage.Prompt,
            missionStage.ExpectedQrHash,
            missionStage.TriviaValidAnswer,
            missionStage.TriviaInitialValidationCriterion,
            missionStage.Hints.Select(hint => hint.ToDomain()).ToArray());
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
            liveSessionStage.TriviaValidAnswer,
            liveSessionStage.TriviaInitialValidationCriterion,
            liveSessionStage.Hints.Select(hint => hint.ToResponse()).ToArray());
    }

    private static LiveSessionStageHint ToDomain(this EligibleMissionStageHintSnapshot hint)
    {
        ArgumentNullException.ThrowIfNull(hint);

        return LiveSessionStageHint.Create(
            hint.Id,
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
