using Umbral.ScoringAudit.Api.Domain.Penalties;
using Umbral.ScoringAudit.Api.Domain.Rankings;
using Umbral.ScoringAudit.Api.Domain.Scoreboards;
using Umbral.ServiceDefaults;
using Xunit;

namespace Umbral.ScoringAudit.Api.Tests;

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
            "Motivo operativo",
            DateTimeOffset.UtcNow));

        Assert.Equal("penalty.unknown_severity", exception.Code);
    }
}

public sealed class ScoreboardStageCreditTests
{
    [Theory]
    [InlineData(MissionStageDifficulty.Easy, 100)]
    [InlineData(MissionStageDifficulty.Medium, 200)]
    [InlineData(MissionStageDifficulty.Hard, 300)]
    public void GrantStageCredit_AwardsFullScoreFromMissionStageDifficulty(
        MissionStageDifficulty difficulty,
        int expectedScore)
    {
        var scoreboard = new Scoreboard(Guid.NewGuid());
        var sessionTeamId = Guid.NewGuid();

        var entry = scoreboard.GrantStageCredit(
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
    public void GrantStageCredit_DoesNotAwardPartialCreditOrDuplicateCredit()
    {
        var scoreboard = new Scoreboard(Guid.NewGuid());
        var sessionTeamId = Guid.NewGuid();
        var missionStageId = Guid.NewGuid();

        var firstEntry = scoreboard.GrantStageCredit(
            sessionTeamId,
            missionStageId,
            MissionStageDifficulty.Medium,
            TimeSpan.FromSeconds(12),
            DateTimeOffset.UtcNow);
        var duplicateEntry = scoreboard.GrantStageCredit(
            sessionTeamId,
            missionStageId,
            MissionStageDifficulty.Medium,
            TimeSpan.FromSeconds(13),
            DateTimeOffset.UtcNow);

        Assert.NotNull(firstEntry);
        Assert.Equal(200, firstEntry.Delta);
        Assert.Null(duplicateEntry);
        Assert.Equal(200, scoreboard.GetTeamScore(sessionTeamId).VisibleScore);
        Assert.Single(scoreboard.ScoreEntries);
    }

    [Fact]
    public void GrantStageCredit_ValidationOverrideAwardsFullDifficultyScoreOnlyOnce()
    {
        var scoreboard = new Scoreboard(Guid.NewGuid());
        var sessionTeamId = Guid.NewGuid();
        var missionStageId = Guid.NewGuid();

        var firstEntry = scoreboard.GrantStageCredit(
            sessionTeamId,
            missionStageId,
            MissionStageDifficulty.Hard,
            TimeSpan.FromSeconds(12),
            DateTimeOffset.UtcNow,
            validationOverride: true);
        var duplicateEntry = scoreboard.GrantStageCredit(
            sessionTeamId,
            missionStageId,
            MissionStageDifficulty.Hard,
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
        Assert.Equal(severity, entry.PenaltySeverity);
    }

    [Fact]
    public void ApplyPenalty_FloorsVisibleScoreButScoreEntryKeepsRealNegativePenaltyDelta()
    {
        var scoreboard = new Scoreboard(Guid.NewGuid());
        var sessionTeamId = Guid.NewGuid();

        scoreboard.GrantStageCredit(
            sessionTeamId,
            Guid.NewGuid(),
            MissionStageDifficulty.Easy,
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

    private static Penalty CreatePenalty(Guid commandId, Guid sessionTeamId, PenaltySeverity severity) =>
        new(
            Guid.NewGuid(),
            commandId,
            sessionTeamId,
            severity,
            "Motivo operativo",
            DateTimeOffset.UtcNow);
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
}