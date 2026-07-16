using SessionManagement.Domain.LiveSessions;
using SessionManagement.Application.Features.SessionLifecycle;

namespace SessionManagement.Application.Features.LiveSessions;

/// <summary>
/// Data required to create a LiveSession from a reusable Mission. The selected Plays are copied
/// into the Session Flow snapshot, so later edits to the Mission never affect this execution.
/// </summary>
/// <param name="MissionId" example="7c3a1b9e-2f4d-4c8a-9b1e-6d5f0a3c8b21">Mission to snapshot. It must still be eligible for a LiveSession, otherwise creation is rejected.</param>
/// <param name="Name" example="Friday night run">Operator-visible name of this execution. Independent of the Mission name, so the same Mission can run many times with distinct names.</param>
/// <param name="ScheduledStartAtUtc" example="2026-07-16T21:00:00Z">Announced start instant. null = no schedule; the session simply waits for the operator to start it and reports no countdown.</param>
/// <param name="SelectedMissionStageIds">Plays to include, in the order they will be played; position defines the 1-based Session Flow order. Ids must belong to the Mission's eligible Plays and cannot repeat. null or empty is rejected: a Session Flow needs at least one Play.</param>
public sealed record CreateLiveSessionRequest(
    Guid MissionId,
    string Name,
    DateTimeOffset? ScheduledStartAtUtc,
    IReadOnlyList<Guid>? SelectedMissionStageIds);

/// <summary>
/// Operator/administrator-facing view of a LiveSession, including its full Session Flow snapshot.
/// </summary>
/// <param name="Id" example="5e1d9f34-8b26-4a7c-9f10-3c2b5d6e7a80">Identifier of this execution; addresses every operator action on the session.</param>
/// <param name="MissionId" example="7c3a1b9e-2f4d-4c8a-9b1e-6d5f0a3c8b21">Mission this execution was snapshotted from.</param>
/// <param name="MissionName" example="Downtown hunt">Mission name as captured when the snapshot was taken; it does not follow later renames of the Mission.</param>
/// <param name="Name" example="Friday night run">Operator-visible name of this execution.</param>
/// <param name="State" example="Scheduled">Session State: Scheduled, Active, Paused, Finalized or Canceled. Controls which operator actions, advances and evidence the session accepts.</param>
/// <param name="ScheduledStartAtUtc" example="2026-07-16T21:00:00Z">Announced start instant. null = the session has no schedule and starts only on operator action.</param>
/// <param name="CreatedAtUtc" example="2026-07-16T18:30:00Z">Instant the snapshot was taken.</param>
/// <param name="JoinCode" example="H7KP2M">Session Join Code participants type to reach this session. null = not generated yet; participants cannot enter until it exists.</param>
/// <param name="EnrollmentWindowOpenedAtUtc" example="2026-07-16T20:45:00Z">Instant the Team Assignment Window opened. null = never opened, so no participant can create or join a Session Team yet.</param>
/// <param name="EnrollmentWindowClosedAtUtc" example="2026-07-16T21:00:00Z">Instant the Team Assignment Window closed. null = it was never closed; combined with a non-null open instant it means team changes are still allowed.</param>
/// <param name="RegisteredSessionTeamCount">Session Teams currently registered in this execution.</param>
/// <param name="SessionStageFlow">Session Flow: the flat, ordered list of Plays this execution runs.</param>
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

/// <summary>
/// Operator/administrator-facing projection of a Play in the Session Flow. Unlike the participant
/// view, it carries the solution-side data (expected QR hash, every Hint) an operator needs to run
/// the session.
/// </summary>
/// <param name="MissionStageId" example="2b8c47f1-9a3e-4d56-b7c8-1e9f0a2d3b64">Play identifier, inherited from the Mission. Addresses this Play when deactivating it or creating Hints for it.</param>
/// <param name="Name" example="Play 3">Operator-facing label of the Play, derived from its order in the Mission.</param>
/// <param name="SessionStageOrder" example="1">Contiguous 1-based position inside this Session Flow. This is the order teams actually play.</param>
/// <param name="SourceOrder" example="4">Original position of the Play in the Mission. Differs from SessionStageOrder when only some Plays were selected, and lets operators trace a Play back to its Mission.</param>
/// <param name="ResolvedTimeBudgetMinutes" example="15">Time limit already resolved at snapshot time; later Mission edits do not change it.</param>
/// <param name="Difficulty" example="Medium">Easy, Medium or Hard. Drives the credit this Play is worth in Scoring and Monitoring.</param>
/// <param name="GameType" example="Trivia">Trivia or Treasure Hunt. Decides which fields below carry data and which evidence endpoint applies.</param>
/// <param name="Prompt" example="Which year was the clock tower built?">Main text shown to participants: the question in Trivia, the instruction or objective in Treasure Hunt.</param>
/// <param name="ExpectedQrHash" example="9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08">Hash of the QR that solves a Treasure Hunt Play. null on Trivia Plays.</param>
/// <param name="Choices">Selectable alternatives on Trivia Plays. Empty on Treasure Hunt. Which one is correct is never exposed here.</param>
/// <param name="Hints">Every Hint authored for this Play, released or not, including solution Hints. Operator-only.</param>
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

/// <summary>
/// A selectable alternative of a Trivia Play.
/// </summary>
/// <param name="Id" example="4d6e2a10-7c95-4b83-a1f2-8e0d9c7b6a53">Value a participant sends back as the selected choice when submitting a Trivia answer.</param>
/// <param name="Text" example="1897">Alternative as displayed. Carries no mark of correctness.</param>
public sealed record LiveSessionChoiceResponse(
    Guid Id,
    string Text);

/// <summary>
/// A Hint of a Play as seen by an operator, whether or not it has been released to any Session Team.
/// </summary>
/// <param name="Id" example="9a0b1c2d-3e4f-4a5b-8c6d-7e8f9a0b1c2d">Addresses this Hint when releasing it to Session Teams.</param>
/// <param name="Content" example="Look behind the fountain">Text delivered to the team once the Hint is released.</param>
/// <param name="IsSolution">true = the Hint reveals the answer, so it stays hidden from participants until the session is Finalized.</param>
/// <param name="Latitude" example="-34.603722">Latitude of the place the Hint points to. null = the Hint has no location and is text only.</param>
/// <param name="Longitude" example="-58.381592">Longitude of the place the Hint points to. null = the Hint has no location and is text only.</param>
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
            liveSession.State.Value,
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
            liveSession.State.Value,
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
