using ScoringAudit.Domain.Penalties;
using Umbral.ServiceDefaults;

namespace ScoringAudit.Domain.Scoreboards;

public sealed class Scoreboard
{
    private readonly Dictionary<Guid, TeamScore> teamScores = [];
    private readonly HashSet<StageCreditKey> creditedStages = [];
    private readonly HashSet<Guid> processedPenaltyCommandIds = [];
    private readonly List<ScoreEntry> scoreEntries = [];

    private Scoreboard()
    {
    }

    public Scoreboard(Guid liveSessionId)
    {
        if (liveSessionId == Guid.Empty)
        {
            throw new UmbralDomainException("scoreboard.empty_live_session_id", "LiveSession id is required.");
        }

        LiveSessionId = liveSessionId;
    }

    public Guid LiveSessionId { get; private set; }

    public IReadOnlyCollection<TeamScore> TeamScores => teamScores.Values.ToArray();

    public IReadOnlyCollection<ScoreEntry> ScoreEntries => scoreEntries.AsReadOnly();

    public void RebuildState()
    {
        teamScores.Clear();
        creditedStages.Clear();
        processedPenaltyCommandIds.Clear();

        foreach (var scoreEntry in scoreEntries.OrderBy(entry => entry.RecordedAt).ThenBy(entry => entry.ScoreEntryId))
        {
            var teamScore = GetOrCreateTeamScore(scoreEntry.SessionTeamId);
            teamScore.Apply(scoreEntry.Delta);

            if (scoreEntry.MissionStageId is not null
                && (scoreEntry.EntryType == ScoreEntryType.StageCredit
                    || scoreEntry.EntryType == ScoreEntryType.ValidationOverrideCredit))
            {
                creditedStages.Add(new StageCreditKey(scoreEntry.SessionTeamId, scoreEntry.MissionStageId.Value));
            }

            if (scoreEntry.EntryType == ScoreEntryType.Penalty
                && scoreEntry.PenaltyCommandId is not null)
            {
                processedPenaltyCommandIds.Add(scoreEntry.PenaltyCommandId.Value);
            }
        }
    }

    public ScoreEntry? GrantStageCredit(
        Guid sessionTeamId,
        Guid missionStageId,
        MissionStageDifficulty difficulty,
        TimeSpan resolutionTime,
        DateTimeOffset recordedAt,
        bool validationOverride = false)
    {
        if (missionStageId == Guid.Empty)
        {
            throw new UmbralDomainException("scoreboard.empty_mission_stage_id", "Mission Stage id is required.");
        }

        if (resolutionTime < TimeSpan.Zero)
        {
            throw new UmbralDomainException("scoreboard.negative_resolution_time", "Resolution Time cannot be negative.");
        }

        EnsureSessionTeamId(sessionTeamId);

        var creditKey = new StageCreditKey(sessionTeamId, missionStageId);
        if (!creditedStages.Add(creditKey))
        {
            return null;
        }

        var entryType = validationOverride
            ? ScoreEntryType.ValidationOverrideCredit
            : ScoreEntryType.StageCredit;

        return ApplyDelta(
            sessionTeamId,
            ScoreFor(difficulty),
            entryType,
            recordedAt,
            missionStageId,
            penaltyCommandId: null,
            penaltyId: null,
            penaltySeverity: null,
            penaltyReason: null,
            appliedByOperatorUserId: null,
            resolutionTime);
    }

    public ScoreEntry ApplyPenalty(Penalty penalty)
    {
        ArgumentNullException.ThrowIfNull(penalty);

        if (!processedPenaltyCommandIds.Add(penalty.CommandId))
        {
            throw new UmbralDomainException(
                "scoreboard.duplicate_penalty_command",
                "Penalty command was already processed for this Scoreboard.");
        }

        return ApplyDelta(
            penalty.SessionTeamId,
            penalty.Delta,
            ScoreEntryType.Penalty,
            penalty.RecordedAt,
            missionStageId: null,
            penalty.CommandId,
            penalty.PenaltyId,
            penalty.Severity,
            penalty.Reason,
            penalty.AppliedByOperatorUserId,
            resolutionTime: null);
    }

    public TeamScore GetTeamScore(Guid sessionTeamId)
    {
        EnsureSessionTeamId(sessionTeamId);
        return GetOrCreateTeamScore(sessionTeamId);
    }

    private ScoreEntry ApplyDelta(
        Guid sessionTeamId,
        int delta,
        ScoreEntryType entryType,
        DateTimeOffset recordedAt,
        Guid? missionStageId,
        Guid? penaltyCommandId,
        Guid? penaltyId,
        PenaltySeverity? penaltySeverity,
        string? penaltyReason,
        string? appliedByOperatorUserId,
        TimeSpan? resolutionTime)
    {
        var teamScore = GetOrCreateTeamScore(sessionTeamId);
        var accumulatedScoreBefore = teamScore.AccumulatedScore;
        var visibleScoreBefore = teamScore.VisibleScore;

        teamScore.Apply(delta);

        var scoreEntry = new ScoreEntry(
            Guid.NewGuid(),
            LiveSessionId,
            sessionTeamId,
            entryType,
            delta,
            accumulatedScoreBefore,
            teamScore.AccumulatedScore,
            visibleScoreBefore,
            teamScore.VisibleScore,
            recordedAt,
            missionStageId,
            penaltyCommandId,
            penaltyId,
            penaltySeverity,
            penaltyReason,
            appliedByOperatorUserId,
            resolutionTime);

        scoreEntries.Add(scoreEntry);
        return scoreEntry;
    }

    private TeamScore GetOrCreateTeamScore(Guid sessionTeamId)
    {
        if (teamScores.TryGetValue(sessionTeamId, out var teamScore))
        {
            return teamScore;
        }

        var createdTeamScore = new TeamScore(sessionTeamId);
        teamScores.Add(sessionTeamId, createdTeamScore);
        return createdTeamScore;
    }

    private static void EnsureSessionTeamId(Guid sessionTeamId)
    {
        if (sessionTeamId == Guid.Empty)
        {
            throw new UmbralDomainException("scoreboard.empty_session_team_id", "Session Team id is required.");
        }
    }

    private static int ScoreFor(MissionStageDifficulty difficulty) => difficulty switch
    {
        MissionStageDifficulty.Easy => 100,
        MissionStageDifficulty.Medium => 200,
        MissionStageDifficulty.Hard => 300,
        _ => throw new UmbralDomainException("scoreboard.unknown_difficulty", "Mission Stage difficulty is not supported.")
    };

    private readonly record struct StageCreditKey(Guid SessionTeamId, Guid MissionStageId);
}

public enum MissionStageDifficulty
{
    Easy,
    Medium,
    Hard
}

public enum ScoreEntryType
{
    StageCredit,
    ValidationOverrideCredit,
    Penalty
}
