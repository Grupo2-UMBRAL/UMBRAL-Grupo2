using ScoringMonitoring.Domain.Penalties;
using Umbral.ServiceDefaults;

namespace ScoringMonitoring.Domain.Scoreboards;

public sealed class Scoreboard
{
    private readonly Dictionary<Guid, TeamScore> teamScores = [];
    private readonly HashSet<PlayCreditKey> creditedPlays = [];
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
        creditedPlays.Clear();
        processedPenaltyCommandIds.Clear();

        foreach (var scoreEntry in scoreEntries.OrderBy(entry => entry.RecordedAt).ThenBy(entry => entry.ScoreEntryId))
        {
            var teamScore = GetOrCreateTeamScore(scoreEntry.SessionTeamId);
            teamScore.Apply(scoreEntry.Delta);

            if (scoreEntry.PlayId is not null
                && (scoreEntry.EntryType == ScoreEntryType.PlayCredit
                    || scoreEntry.EntryType == ScoreEntryType.ValidationOverrideCredit))
            {
                creditedPlays.Add(new PlayCreditKey(scoreEntry.SessionTeamId, scoreEntry.PlayId.Value));
            }

            if (scoreEntry.EntryType == ScoreEntryType.Penalty
                && scoreEntry.PenaltyCommandId is not null)
            {
                processedPenaltyCommandIds.Add(scoreEntry.PenaltyCommandId.Value);
            }
        }
    }

    public ScoreEntry? GrantPlayCredit(
        Guid sessionTeamId,
        Guid playId,
        PlayDifficulty difficulty,
        TimeSpan resolutionTime,
        DateTimeOffset recordedAt,
        bool validationOverride = false)
    {
        if (playId == Guid.Empty)
        {
            throw new UmbralDomainException("scoreboard.empty_play_id", "Play id is required.");
        }

        if (resolutionTime < TimeSpan.Zero)
        {
            throw new UmbralDomainException("scoreboard.negative_resolution_time", "Resolution Time cannot be negative.");
        }

        EnsureSessionTeamId(sessionTeamId);

        var creditKey = new PlayCreditKey(sessionTeamId, playId);
        if (!creditedPlays.Add(creditKey))
        {
            return null;
        }

        var entryType = validationOverride
            ? ScoreEntryType.ValidationOverrideCredit
            : ScoreEntryType.PlayCredit;

        return ApplyDelta(
            sessionTeamId,
            ScoreFor(difficulty),
            entryType,
            recordedAt,
            playId,
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
            playId: null,
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
        Guid? playId,
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
            playId,
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

    private static int ScoreFor(PlayDifficulty difficulty) => difficulty switch
    {
        PlayDifficulty.Easy => 100,
        PlayDifficulty.Medium => 200,
        PlayDifficulty.Hard => 300,
        _ => throw new UmbralDomainException("scoreboard.unknown_difficulty", "Play difficulty is not supported.")
    };

    private readonly record struct PlayCreditKey(Guid SessionTeamId, Guid PlayId);
}

public enum PlayDifficulty
{
    Easy,
    Medium,
    Hard
}

public enum ScoreEntryType
{
    PlayCredit,
    ValidationOverrideCredit,
    Penalty
}
