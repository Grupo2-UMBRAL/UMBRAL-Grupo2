using Umbral.ScoringAudit.Api.Domain.Penalties;
using Umbral.ServiceDefaults;

namespace Umbral.ScoringAudit.Api.Domain.Scoreboards;

public sealed class ScoreEntry
{
    private ScoreEntry()
    {
    }

    public ScoreEntry(
        Guid scoreEntryId,
        Guid liveSessionId,
        Guid sessionTeamId,
        ScoreEntryType entryType,
        int delta,
        int accumulatedScoreBefore,
        int accumulatedScoreAfter,
        int visibleScoreBefore,
        int visibleScoreAfter,
        DateTimeOffset recordedAt,
        Guid? missionStageId = null,
        Guid? penaltyCommandId = null,
        Guid? penaltyId = null,
        PenaltySeverity? penaltySeverity = null,
        string? penaltyReason = null,
        string? appliedByOperatorUserId = null,
        TimeSpan? resolutionTime = null)
    {
        if (scoreEntryId == Guid.Empty)
        {
            throw new UmbralDomainException("score_entry.empty_id", "Score Entry id is required.");
        }

        if (liveSessionId == Guid.Empty)
        {
            throw new UmbralDomainException("score_entry.empty_live_session_id", "LiveSession id is required.");
        }

        if (sessionTeamId == Guid.Empty)
        {
            throw new UmbralDomainException("score_entry.empty_session_team_id", "Session Team id is required.");
        }

        if (visibleScoreBefore < 0 || visibleScoreAfter < 0)
        {
            throw new UmbralDomainException("score_entry.negative_visible_score", "Visible score cannot be negative.");
        }

        ScoreEntryId = scoreEntryId;
        LiveSessionId = liveSessionId;
        SessionTeamId = sessionTeamId;
        EntryType = entryType;
        Delta = delta;
        AccumulatedScoreBefore = accumulatedScoreBefore;
        AccumulatedScoreAfter = accumulatedScoreAfter;
        VisibleScoreBefore = visibleScoreBefore;
        VisibleScoreAfter = visibleScoreAfter;
        RecordedAt = recordedAt;
        MissionStageId = missionStageId;
        PenaltyCommandId = penaltyCommandId;
        PenaltyId = penaltyId;
        PenaltySeverity = penaltySeverity;
        PenaltyReason = penaltyReason;
        AppliedByOperatorUserId = appliedByOperatorUserId;
        ResolutionTime = resolutionTime;
    }

    public Guid ScoreEntryId { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public Guid SessionTeamId { get; private set; }

    public ScoreEntryType EntryType { get; private set; }

    public int Delta { get; private set; }

    public int AccumulatedScoreBefore { get; private set; }

    public int AccumulatedScoreAfter { get; private set; }

    public int VisibleScoreBefore { get; private set; }

    public int VisibleScoreAfter { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public Guid? MissionStageId { get; private set; }

    public Guid? PenaltyCommandId { get; private set; }

    public Guid? PenaltyId { get; private set; }

    public PenaltySeverity? PenaltySeverity { get; private set; }

    public string? PenaltyReason { get; private set; }

    public string? AppliedByOperatorUserId { get; private set; }

    public TimeSpan? ResolutionTime { get; private set; }
}
