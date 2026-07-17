using System.Reflection;
using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.UnitTests;

// Builder helpers for LiveSession aggregate tests. Mirrors the spirit of
// MissionManagement.UnitTests/SampleMissions.cs: real factory methods only,
// no reflection except the State backing-field poke (LiveSession has no public
// state setter, so guard tests that need a specific state force it directly).
internal static class SampleLiveSessions
{
    public const string JoinCodeText = "ABCD23";

    public static readonly DateTimeOffset CreatedAt = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Now = new(2026, 6, 3, 14, 0, 0, TimeSpan.Zero);

    public static readonly Guid MissionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid CorrectChoiceId = Guid.Parse("c0c0c0c0-0000-0000-0000-000000000001");
    public static readonly Guid WrongChoiceId = Guid.Parse("c0c0c0c0-0000-0000-0000-000000000002");

    public static Guid StageId(int order) => Guid.Parse($"dddddddd-dddd-dddd-dddd-{order:000000000000}");

    public static Guid TeamId(int index = 1) => Guid.Parse($"bbbbbbbb-bbbb-bbbb-bbbb-{index:000000000000}");

    public static Guid HintId(int index = 1) => Guid.Parse($"11111111-1111-1111-1111-{index:000000000000}");

    public static JoinCode JoinCode() => Domain.LiveSessions.JoinCode.Parse(JoinCodeText);

    public static LiveSessionStageHint Hint(int index = 1)
        => LiveSessionStageHint.Create(HintId(index), $"Hint {index}", isSolution: false);

    public static LiveSessionStageHint SolutionHint(int index = 1)
        => LiveSessionStageHint.Create(HintId(index), $"Solution {index}", isSolution: true);

    public static LiveSessionStage TreasureStage(
        int order,
        string? expectedQrHash = null,
        IReadOnlyList<LiveSessionStageHint>? hints = null)
        => LiveSessionStage.Create(
            StageId(order),
            $"Stage {order}",
            order,
            order,
            10,
            "Medium",
            "TreasureHunt",
            $"Prompt for Stage {order}",
            expectedQrHash: expectedQrHash ?? $"qr-stage-{order}",
            hints: hints);

    public static LiveSessionStage TriviaStage(int order, bool withCorrectChoice = true)
        => LiveSessionStage.Create(
            StageId(order),
            $"Trivia Stage {order}",
            order,
            order,
            10,
            "Medium",
            "Trivia",
            $"Prompt for Trivia Stage {order}",
            choices:
            [
                LiveSessionChoice.Create(CorrectChoiceId, "Caracas"),
                LiveSessionChoice.Create(WrongChoiceId, "Valencia")
            ],
            correctChoiceId: withCorrectChoice ? CorrectChoiceId : null);

    public static IReadOnlyList<LiveSessionStage> TreasureStages(int count)
        => Enumerable.Range(1, count).Select(order => TreasureStage(order)).ToArray();

    public static IReadOnlyList<LiveSessionStage> TriviaStages(int count)
        => Enumerable.Range(1, count).Select(order => TriviaStage(order)).ToArray();

    public static LiveSession Create(IReadOnlyList<LiveSessionStage> stages, Guid? id = null)
        => LiveSession.Create(
            id ?? Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            MissionId,
            "Mission",
            "Session",
            scheduledStartAtUtc: null,
            createdAtUtc: CreatedAt,
            sessionStageFlow: stages);

    // Scheduled session with N treasure stages and M teams attached directly
    // (no enrollment ceremony — sufficient for evidence/hint/stage guard tests
    // since those paths do not require Scheduled state).
    public static LiveSession WithTeams(
        int teamCount = 1,
        IReadOnlyList<LiveSessionStage>? stages = null)
    {
        var liveSession = Create(stages ?? TreasureStages(2));
        for (var index = 1; index <= teamCount; index++)
        {
            liveSession.SessionTeams.Add(
                SessionTeam.Create(liveSession.Id, TeamId(index), $"Team {index}", CreatedAt.AddMinutes(-20)));
        }

        return liveSession;
    }

    // Same as WithTeams but forced into a target state for guard coverage.
    public static LiveSession InState(
        string state,
        int teamCount = 1,
        IReadOnlyList<LiveSessionStage>? stages = null)
    {
        var liveSession = WithTeams(teamCount, stages);
        ForceState(liveSession, state);
        return liveSession;
    }

    // Scheduled session (with an optional scheduled start) and directly-attached
    // teams — used to drive overview countdown branches.
    public static LiveSession ScheduledWithStart(
        DateTimeOffset? scheduledStartAtUtc,
        int teamCount = 1,
        IReadOnlyList<LiveSessionStage>? stages = null)
    {
        var liveSession = LiveSession.Create(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            MissionId,
            "Mission",
            "Session",
            scheduledStartAtUtc,
            CreatedAt,
            stages ?? TreasureStages(2));
        for (var index = 1; index <= teamCount; index++)
        {
            liveSession.SessionTeams.Add(
                SessionTeam.Create(liveSession.Id, TeamId(index), $"Team {index}", CreatedAt.AddMinutes(-20)));
        }

        return liveSession;
    }

    // Full real enrollment-to-active flow with one enrolled participant.
    public static LiveSession EnrolledActive(IReadOnlyList<LiveSessionStage>? stages = null)
    {
        var liveSession = Create(stages ?? TreasureStages(2));
        liveSession.AssignJoinCode(JoinCode());
        liveSession.OpenEnrollmentWindow(CreatedAt.AddMinutes(5));
        var team = liveSession.RegisterTeam(TeamId(1), "Team 1", JoinCode(), CreatedAt.AddMinutes(10));
        liveSession.EnrollParticipantInTeam(team.Id, "participant-1", JoinCode(), CreatedAt.AddMinutes(10));
        liveSession.Start(CreatedAt.AddMinutes(20));
        return liveSession;
    }

    public static void ForceState(LiveSession liveSession, string state)
    {
        var targetState = LiveSessionState.FromName(state);
        if (liveSession.State == targetState) return;

        var now = DateTimeOffset.UtcNow;
        
        // Base requirements to start a session
        if (liveSession.State == LiveSessionState.Scheduled)
        {
            if (string.IsNullOrEmpty(liveSession.JoinCodeValue))
                liveSession.AssignJoinCode(JoinCode());
            
            if (liveSession.EnrollmentWindowOpenedAtUtc == null)
                liveSession.OpenEnrollmentWindow(now.AddHours(-1));
                
            if (!liveSession.SessionTeams.Any())
            {
                 var team = liveSession.RegisterTeam(TeamId(1), "Team 1", JoinCode(), now);
                 liveSession.EnrollParticipantInTeam(team.Id, "participant-1", JoinCode(), now);
            }
            liveSession.Start(now);
        }

        if (targetState == LiveSessionState.Active)
        {
            liveSession.ClearDomainEvents();
            return;
        }

        if (targetState == LiveSessionState.Paused)
        {
            liveSession.Pause();
            liveSession.ClearDomainEvents();
            return;
        }
        
        if (targetState == LiveSessionState.Canceled)
        {
            liveSession.Cancel();
            liveSession.ClearDomainEvents();
            return;
        }

        if (targetState == LiveSessionState.Finalized)
        {
            // First we need to make sure we are not already canceled
            if (liveSession.State != LiveSessionState.Canceled)
            {
                // Can only finalize if in progress or paused
                if (liveSession.State == LiveSessionState.Scheduled) liveSession.Start(now);
                liveSession.FinalizeSession();
            }
            liveSession.ClearDomainEvents();
            return;
        }

        throw new NotSupportedException($"Cannot force transition to state {state}");
    }
}
