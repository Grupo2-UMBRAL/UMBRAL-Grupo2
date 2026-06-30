using ScoringMonitoring.Domain.Penalties;
using ScoringMonitoring.Domain.Rankings;
using ScoringMonitoring.Domain.Audit;
using ScoringMonitoring.Domain.Scoreboards;
using ScoringMonitoring.Application.Features.Rankings;
using Umbral.ServiceDefaults;
using Xunit;

namespace ScoringMonitoring.UnitTests;

public sealed class SessionEventLogTests
{
    [Fact]
    public void Constructor_CreatesAuditableSessionEvent()
    {
        var liveSessionId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        var eventLog = new SessionEventLog(
            Guid.NewGuid(),
            liveSessionId,
            " StageCredit ",
            " Session Team completed a stage. ",
            timestamp);

        Assert.Equal(liveSessionId, eventLog.LiveSessionId);
        Assert.Equal("StageCredit", eventLog.EventType);
        Assert.Equal("Session Team completed a stage.", eventLog.Description);
        Assert.Equal(timestamp, eventLog.Timestamp);
    }

    [Fact]
    public void Constructor_RejectsMissingEventType()
    {
        var exception = Assert.Throws<UmbralDomainException>(() => new SessionEventLog(
            Guid.NewGuid(),
            Guid.NewGuid(),
            " ",
            "Session Team completed a stage.",
            DateTimeOffset.UtcNow));

        Assert.Equal("session_event_log.empty_event_type", exception.Code);
    }

    [Fact]
    public void Constructor_RejectsMissingDescription()
    {
        var exception = Assert.Throws<UmbralDomainException>(() => new SessionEventLog(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "StageCredit",
            " ",
            DateTimeOffset.UtcNow));

        Assert.Equal("session_event_log.empty_description", exception.Code);
    }
}

public sealed class PenaltySeverityTests
{
    [Theory]
    [InlineData(PenaltySeverity.Minor, -50)]
    [InlineData(PenaltySeverity.Major, -100)]
    [InlineData(PenaltySeverity.Critical, -200)]
    public void Values_AreFixedScoreDeltas(PenaltySeverity severity, int expectedDelta)
    {
        Assert.Equal(expectedDelta, (int)severity);
    }

    [Fact]
    public void Constructor_RejectsUnknownSeverity()
    {
        var exception = Assert.Throws<UmbralDomainException>(() => new Penalty(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            (PenaltySeverity)(-999),
            "operator-1",
            "Motivo operativo",
            DateTimeOffset.UtcNow));

        Assert.Equal("penalty.unknown_severity", exception.Code);
    }

    [Fact]
    public void Constructor_RequiresReason()
    {
        var exception = Assert.Throws<UmbralDomainException>(() => new Penalty(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            PenaltySeverity.Minor,
            "operator-1",
            " ",
            DateTimeOffset.UtcNow));

        Assert.Equal("penalty.reason_required", exception.Code);
    }
}

public sealed class ScoreboardStageCreditTests
{
    [Theory]
    [InlineData(PlayDifficulty.Easy, 100)]
    [InlineData(PlayDifficulty.Medium, 200)]
    [InlineData(PlayDifficulty.Hard, 300)]
    public void GrantPlayCredit_AwardsFullScoreFromPlayDifficulty(
        PlayDifficulty difficulty,
        int expectedScore)
    {
        var scoreboard = new Scoreboard(Guid.NewGuid());
        var sessionTeamId = Guid.NewGuid();

        var entry = scoreboard.GrantPlayCredit(
            sessionTeamId,
            Guid.NewGuid(),
            difficulty,
            TimeSpan.FromSeconds(12),
            DateTimeOffset.UtcNow);

        Assert.NotNull(entry);
        Assert.Equal(expectedScore, entry.Delta);
        Assert.Equal(expectedScore, entry.AccumulatedScoreAfter);
        Assert.Equal(expectedScore, entry.VisibleScoreAfter);
        Assert.Equal(expectedScore, scoreboard.GetTeamScore(sessionTeamId).VisibleScore);
    }

    [Fact]
    public void GrantPlayCredit_DoesNotAwardPartialCreditOrDuplicateCredit()
    {
        var scoreboard = new Scoreboard(Guid.NewGuid());
        var sessionTeamId = Guid.NewGuid();
        var missionStageId = Guid.NewGuid();

        var firstEntry = scoreboard.GrantPlayCredit(
            sessionTeamId,
            missionStageId,
            PlayDifficulty.Medium,
            TimeSpan.FromSeconds(12),
            DateTimeOffset.UtcNow);
        var duplicateEntry = scoreboard.GrantPlayCredit(
            sessionTeamId,
            missionStageId,
            PlayDifficulty.Medium,
            TimeSpan.FromSeconds(13),
            DateTimeOffset.UtcNow);

        Assert.NotNull(firstEntry);
        Assert.Equal(200, firstEntry.Delta);
        Assert.Null(duplicateEntry);
        Assert.Equal(200, scoreboard.GetTeamScore(sessionTeamId).VisibleScore);
        Assert.Single(scoreboard.ScoreEntries);
    }

    [Fact]
    public void GrantPlayCredit_ValidationOverrideAwardsFullDifficultyScoreOnlyOnce()
    {
        var scoreboard = new Scoreboard(Guid.NewGuid());
        var sessionTeamId = Guid.NewGuid();
        var missionStageId = Guid.NewGuid();

        var firstEntry = scoreboard.GrantPlayCredit(
            sessionTeamId,
            missionStageId,
            PlayDifficulty.Hard,
            TimeSpan.FromSeconds(12),
            DateTimeOffset.UtcNow,
            validationOverride: true);
        var duplicateEntry = scoreboard.GrantPlayCredit(
            sessionTeamId,
            missionStageId,
            PlayDifficulty.Hard,
            TimeSpan.FromSeconds(13),
            DateTimeOffset.UtcNow,
            validationOverride: true);

        Assert.NotNull(firstEntry);
        Assert.Equal(300, firstEntry.Delta);
        Assert.Equal(ScoreEntryType.ValidationOverrideCredit, firstEntry.EntryType);
        Assert.Null(duplicateEntry);
        Assert.Equal(300, scoreboard.GetTeamScore(sessionTeamId).VisibleScore);
        Assert.Single(scoreboard.ScoreEntries);
    }

    [Fact]
    public void RebuildState_ReconstructsTeamScoresAndCreditedStagesFromScoreEntries()
    {
        var scoreboard = new Scoreboard(Guid.NewGuid());
        var sessionTeamId = Guid.NewGuid();
        var missionStageId = Guid.NewGuid();

        scoreboard.GrantPlayCredit(
            sessionTeamId,
            missionStageId,
            PlayDifficulty.Easy,
            TimeSpan.FromSeconds(8),
            DateTimeOffset.UtcNow);

        scoreboard.RebuildState();
        var duplicateEntry = scoreboard.GrantPlayCredit(
            sessionTeamId,
            missionStageId,
            PlayDifficulty.Easy,
            TimeSpan.FromSeconds(9),
            DateTimeOffset.UtcNow);

        Assert.Null(duplicateEntry);
        Assert.Equal(100, scoreboard.GetTeamScore(sessionTeamId).VisibleScore);
        Assert.Single(scoreboard.ScoreEntries);
    }

    [Fact]
    public void RebuildState_ReconstructsVisibleScoresAndSingleStageCreditMarkersAfterMaterialization()
    {
        var scoreboard = new Scoreboard(Guid.NewGuid());
        var firstTeamId = Guid.NewGuid();
        var secondTeamId = Guid.NewGuid();
        var firstStageId = Guid.NewGuid();
        var secondStageId = Guid.NewGuid();

        scoreboard.GrantPlayCredit(
            firstTeamId,
            firstStageId,
            PlayDifficulty.Easy,
            TimeSpan.FromSeconds(8),
            DateTimeOffset.UtcNow);
        scoreboard.GrantPlayCredit(
            secondTeamId,
            secondStageId,
            PlayDifficulty.Medium,
            TimeSpan.FromSeconds(9),
            DateTimeOffset.UtcNow);

        ClearMaterializedState(scoreboard);

        scoreboard.RebuildState();
        var duplicateEntry = scoreboard.GrantPlayCredit(
            firstTeamId,
            firstStageId,
            PlayDifficulty.Easy,
            TimeSpan.FromSeconds(10),
            DateTimeOffset.UtcNow);

        Assert.Equal(100, scoreboard.GetTeamScore(firstTeamId).VisibleScore);
        Assert.Equal(200, scoreboard.GetTeamScore(secondTeamId).VisibleScore);
        Assert.Null(duplicateEntry);
        Assert.Equal(2, scoreboard.ScoreEntries.Count);
    }

    private static void ClearMaterializedState(Scoreboard scoreboard)
    {
        ClearPrivateCollection(scoreboard, "teamScores");
        ClearPrivateCollection(scoreboard, "creditedPlays");
        ClearPrivateCollection(scoreboard, "processedPenaltyCommandIds");
    }

    private static void ClearPrivateCollection(Scoreboard scoreboard, string fieldName)
    {
        var field = typeof(Scoreboard).GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var value = field?.GetValue(scoreboard);
        value?.GetType().GetMethod("Clear", Type.EmptyTypes)?.Invoke(value, []);
    }
}

public sealed class ScoreboardPenaltyTests
{
    [Theory]
    [InlineData(PenaltySeverity.Minor, -50)]
    [InlineData(PenaltySeverity.Major, -100)]
    [InlineData(PenaltySeverity.Critical, -200)]
    public void ApplyPenalty_DiscountsFixedSeverityAndFloorsVisibleScoreWithoutSaturatingAudit(
        PenaltySeverity severity,
        int expectedDelta)
    {
        var scoreboard = new Scoreboard(Guid.NewGuid());
        var sessionTeamId = Guid.NewGuid();
        var penalty = CreatePenalty(Guid.NewGuid(), sessionTeamId, severity);

        var entry = scoreboard.ApplyPenalty(penalty);
        var teamScore = scoreboard.GetTeamScore(sessionTeamId);

        Assert.Equal(expectedDelta, entry.Delta);
        Assert.Equal(expectedDelta, entry.AccumulatedScoreAfter);
        Assert.Equal(0, entry.VisibleScoreAfter);
        Assert.Equal(expectedDelta, teamScore.AccumulatedScore);
        Assert.Equal(0, teamScore.VisibleScore);
        Assert.Equal(penalty.CommandId, entry.PenaltyCommandId);
        Assert.Equal(severity, entry.PenaltySeverity);
        Assert.Equal("operator-1", entry.AppliedByOperatorUserId);
        Assert.Equal("Motivo operativo", entry.PenaltyReason);
    }

    [Fact]
    public void ApplyPenalty_FloorsVisibleScoreButScoreEntryKeepsRealNegativePenaltyDelta()
    {
        var scoreboard = new Scoreboard(Guid.NewGuid());
        var sessionTeamId = Guid.NewGuid();

        scoreboard.GrantPlayCredit(
            sessionTeamId,
            Guid.NewGuid(),
            PlayDifficulty.Easy,
            TimeSpan.FromSeconds(2),
            DateTimeOffset.UtcNow);

        var penalty = CreatePenalty(Guid.NewGuid(), sessionTeamId, PenaltySeverity.Critical);
        var entry = scoreboard.ApplyPenalty(penalty);
        var teamScore = scoreboard.GetTeamScore(sessionTeamId);

        Assert.Equal(-200, entry.Delta);
        Assert.Equal(100, entry.AccumulatedScoreBefore);
        Assert.Equal(-100, entry.AccumulatedScoreAfter);
        Assert.Equal(100, entry.VisibleScoreBefore);
        Assert.Equal(0, entry.VisibleScoreAfter);
        Assert.Equal(-100, teamScore.AccumulatedScore);
        Assert.Equal(0, teamScore.VisibleScore);
    }

    [Fact]
    public void ApplyPenalty_BlocksDuplicatePenaltyCommand()
    {
        var scoreboard = new Scoreboard(Guid.NewGuid());
        var sessionTeamId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var firstPenalty = CreatePenalty(commandId, sessionTeamId, PenaltySeverity.Minor);
        var replayedPenalty = CreatePenalty(commandId, sessionTeamId, PenaltySeverity.Minor);

        scoreboard.ApplyPenalty(firstPenalty);

        var exception = Assert.Throws<UmbralDomainException>(() => scoreboard.ApplyPenalty(replayedPenalty));
        Assert.Equal("scoreboard.duplicate_penalty_command", exception.Code);
    }

    [Fact]
    public void RebuildState_ReconstructsProcessedPenaltyCommandMarkers()
    {
        var scoreboard = new Scoreboard(Guid.NewGuid());
        var sessionTeamId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var firstPenalty = CreatePenalty(commandId, sessionTeamId, PenaltySeverity.Minor);
        var replayedPenalty = CreatePenalty(commandId, sessionTeamId, PenaltySeverity.Minor);

        scoreboard.ApplyPenalty(firstPenalty);
        ClearMaterializedState(scoreboard);

        scoreboard.RebuildState();

        var exception = Assert.Throws<UmbralDomainException>(() => scoreboard.ApplyPenalty(replayedPenalty));
        Assert.Equal("scoreboard.duplicate_penalty_command", exception.Code);
    }

    private static Penalty CreatePenalty(Guid commandId, Guid sessionTeamId, PenaltySeverity severity) =>
        new(
            Guid.NewGuid(),
            commandId,
            sessionTeamId,
            severity,
            "operator-1",
            "Motivo operativo",
            DateTimeOffset.UtcNow);

    private static void ClearMaterializedState(Scoreboard scoreboard)
    {
        ClearPrivateCollection(scoreboard, "teamScores");
        ClearPrivateCollection(scoreboard, "creditedPlays");
        ClearPrivateCollection(scoreboard, "processedPenaltyCommandIds");
    }

    private static void ClearPrivateCollection(Scoreboard scoreboard, string fieldName)
    {
        var field = typeof(Scoreboard).GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var value = field?.GetValue(scoreboard);
        value?.GetType().GetMethod("Clear", Type.EmptyTypes)?.Invoke(value, []);
    }
}

public sealed class RankingComparerTests
{
    [Fact]
    public void Compare_OrdersByVisibleScoreDescending()
    {
        var comparer = new RankingComparer();
        var highScore = new RankingEntry(Guid.NewGuid(), 200, TimeSpan.FromSeconds(10));
        var lowScore = new RankingEntry(Guid.NewGuid(), 100, TimeSpan.FromSeconds(1));

        Assert.True(comparer.Compare(highScore, lowScore) < 0);
    }

    [Fact]
    public void Compare_KeepsTieWhenResolutionTimesDifferBy499MillisecondsWithinSameBucket()
    {
        var comparer = new RankingComparer();
        var firstTeam = new RankingEntry(Guid.NewGuid(), 100, TimeSpan.FromMilliseconds(1000));
        var secondTeam = new RankingEntry(Guid.NewGuid(), 100, TimeSpan.FromMilliseconds(1499));

        Assert.Equal(0, comparer.Compare(firstTeam, secondTeam));
        Assert.Equal(0, comparer.Compare(secondTeam, firstTeam));
    }

    [Fact]
    public void Compare_FasterTeamWinsWhenResolutionTimesDifferBy501MillisecondsAcrossBuckets()
    {
        var comparer = new RankingComparer();
        var fasterTeam = new RankingEntry(Guid.NewGuid(), 100, TimeSpan.FromMilliseconds(1000));
        var slowerTeam = new RankingEntry(Guid.NewGuid(), 100, TimeSpan.FromMilliseconds(1501));

        Assert.True(comparer.Compare(fasterTeam, slowerTeam) < 0);
        Assert.True(comparer.Compare(slowerTeam, fasterTeam) > 0);
    }

    [Fact]
    public void Projection_AssignsStandardCompetitionRankingWhenTeamsShareRank()
    {
        var scoreboard = new Scoreboard(Guid.NewGuid());
        var firstTeamId = Guid.NewGuid();
        var secondTeamId = Guid.NewGuid();
        var thirdTeamId = Guid.NewGuid();

        scoreboard.GrantPlayCredit(
            firstTeamId,
            Guid.NewGuid(),
            PlayDifficulty.Easy,
            TimeSpan.FromMilliseconds(1000),
            DateTimeOffset.UtcNow);
        scoreboard.GrantPlayCredit(
            secondTeamId,
            Guid.NewGuid(),
            PlayDifficulty.Easy,
            TimeSpan.FromMilliseconds(1499),
            DateTimeOffset.UtcNow);
        scoreboard.GrantPlayCredit(
            thirdTeamId,
            Guid.NewGuid(),
            PlayDifficulty.Easy,
            TimeSpan.FromMilliseconds(2500),
            DateTimeOffset.UtcNow);

        var ranking = RankingProjection.Create(scoreboard, DateTimeOffset.UtcNow);

        Assert.Collection(
            ranking.Items,
            first =>
            {
                Assert.Equal(1, first.Rank);
                Assert.Equal(firstTeamId, first.SessionTeamId);
            },
            second =>
            {
                Assert.Equal(1, second.Rank);
                Assert.Equal(secondTeamId, second.SessionTeamId);
            },
            third =>
            {
                Assert.Equal(3, third.Rank);
                Assert.Equal(thirdTeamId, third.SessionTeamId);
            });
    }

    [Fact]
    public void Projection_OrdersEqualScoresByResolutionTimeBucket()
    {
        var scoreboard = new Scoreboard(Guid.NewGuid());
        var fasterTeamId = Guid.NewGuid();
        var slowerTeamId = Guid.NewGuid();

        scoreboard.GrantPlayCredit(
            slowerTeamId,
            Guid.NewGuid(),
            PlayDifficulty.Medium,
            TimeSpan.FromMilliseconds(1501),
            DateTimeOffset.UtcNow);
        scoreboard.GrantPlayCredit(
            fasterTeamId,
            Guid.NewGuid(),
            PlayDifficulty.Medium,
            TimeSpan.FromMilliseconds(1000),
            DateTimeOffset.UtcNow);

        var ranking = RankingProjection.Create(scoreboard, DateTimeOffset.UtcNow);

        Assert.Collection(
            ranking.Items,
            first =>
            {
                Assert.Equal(1, first.Rank);
                Assert.Equal(fasterTeamId, first.SessionTeamId);
            },
            second =>
            {
                Assert.Equal(2, second.Rank);
                Assert.Equal(slowerTeamId, second.SessionTeamId);
            });
    }
}
