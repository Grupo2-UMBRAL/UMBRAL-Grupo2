using Microsoft.EntityFrameworkCore;
using ScoringMonitoring.Application.Features.SessionEventLogs;
using ScoringMonitoring.Application.Features.SessionEventLogs.Commands.LogSessionEvent;
using ScoringMonitoring.Application.Features.SessionEventLogs.Queries.GetSessionEventLog;
using ScoringMonitoring.Application.Features.Rankings;
using ScoringMonitoring.Application.Features.Scoreboards;
using ScoringMonitoring.Application.Features.Scoreboards.Commands.ApplyPenalty;
using ScoringMonitoring.Application.Features.Scoreboards.Commands.RecordStageCredit;
using ScoringMonitoring.Domain.Audit;
using ScoringMonitoring.Infrastructure.Persistence;
using Xunit;

namespace ScoringMonitoring.Api.Tests;

public sealed class SessionEventLogApplicationTests
{
    [Fact]
    public async Task LogSessionEventCommand_PersistsEventLogAndPublishesRealtimePayload()
    {
        await using var dbContext = CreateDbContext();
        var updatesPublisher = new CapturingScoringMonitoringUpdatesPublisher();
        var handler = new LogSessionEventHandler(
            dbContext,
            new Repository<SessionEventLog>(dbContext),
            TimeProvider.System,
            updatesPublisher);
        var liveSessionId = Guid.NewGuid();

        var payload = await handler.Handle(
            new LogSessionEventCommand(liveSessionId, "OperatorNote", "Operador marco control manual."),
            CancellationToken.None);

        var persisted = await dbContext.SessionEventLogs.SingleAsync();
        Assert.Equal(payload.Id, persisted.Id);
        Assert.Equal(liveSessionId, persisted.LiveSessionId);
        Assert.Equal("OperatorNote", persisted.EventType);
        Assert.Equal("Operador marco control manual.", persisted.Description);
        Assert.Single(updatesPublisher.EventLogPayloads);
        Assert.Equal(payload, updatesPublisher.EventLogPayloads[0]);
    }

    [Fact]
    public async Task GetSessionEventLogQuery_ReturnsLiveSessionEventsOrderedByTimestampDescending()
    {
        await using var dbContext = CreateDbContext();
        var liveSessionId = Guid.NewGuid();
        var otherLiveSessionId = Guid.NewGuid();
        var oldest = new SessionEventLog(
            Guid.NewGuid(),
            liveSessionId,
            "Oldest",
            "Primer evento.",
            DateTimeOffset.Parse("2026-06-04T01:00:00Z"));
        var newest = new SessionEventLog(
            Guid.NewGuid(),
            liveSessionId,
            "Newest",
            "Ultimo evento.",
            DateTimeOffset.Parse("2026-06-04T01:30:00Z"));
        var otherSessionEvent = new SessionEventLog(
            Guid.NewGuid(),
            otherLiveSessionId,
            "Other",
            "Evento de otra LiveSession.",
            DateTimeOffset.Parse("2026-06-04T02:00:00Z"));
        dbContext.SessionEventLogs.AddRange(oldest, newest, otherSessionEvent);
        await dbContext.SaveChangesAsync();
        var handler = new GetSessionEventLogHandler(dbContext, new Repository<SessionEventLog>(dbContext));

        var payload = await handler.Handle(new GetSessionEventLogQuery(liveSessionId), CancellationToken.None);

        Assert.Collection(
            payload,
            first => Assert.Equal(newest.Id, first.Id),
            second => Assert.Equal(oldest.Id, second.Id));
    }

    [Fact]
    public async Task GetSessionEventLogQuery_ReturnsEmptyListWhenSessionHasNoEvents()
    {
        await using var dbContext = CreateDbContext();
        var handler = new GetSessionEventLogHandler(dbContext, new Repository<SessionEventLog>(dbContext));

        var payload = await handler.Handle(new GetSessionEventLogQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(payload);
    }

    [Fact]
    public async Task RecordStageCreditCommand_PersistsStageCreditAuditEvent()
    {
        await using var dbContext = CreateDbContext();
        var updatesPublisher = new CapturingScoringMonitoringUpdatesPublisher();
        var handler = new RecordStageCreditHandler(
            dbContext,
            new Repository<Scoreboard>(dbContext),
            new Repository<SessionEventLog>(dbContext),
            TimeProvider.System,
            updatesPublisher);
        var liveSessionId = Guid.NewGuid();
        var sessionTeamId = Guid.NewGuid();
        var missionStageId = Guid.NewGuid();

        await handler.Handle(
            new RecordStageCreditCommand(
                liveSessionId,
                sessionTeamId,
                missionStageId,
                "Medium",
                TimeSpan.FromSeconds(15),
                DateTimeOffset.Parse("2026-06-04T01:45:00Z"),
                ValidationOverride: false),
            CancellationToken.None);

        var persistedEvent = await dbContext.SessionEventLogs.SingleAsync();
        Assert.Equal(liveSessionId, persistedEvent.LiveSessionId);
        Assert.Equal("StageCredit", persistedEvent.EventType);
        Assert.Contains(sessionTeamId.ToString(), persistedEvent.Description);
        Assert.Contains(missionStageId.ToString(), persistedEvent.Description);
        Assert.Contains("200", persistedEvent.Description);
        Assert.Single(updatesPublisher.EventLogPayloads);
        Assert.Equal(persistedEvent.Id, updatesPublisher.EventLogPayloads[0].Id);
    }

    [Fact]
    public async Task ApplyPenaltyCommand_PersistsPenaltyAppliedAuditEventAndPublishesRealtimePayload()
    {
        await using var dbContext = CreateDbContext();
        var updatesPublisher = new CapturingScoringMonitoringUpdatesPublisher();
        var handler = new ApplyPenaltyHandler(
            new ApplyPenaltyScoreboardStore(dbContext),
            TimeProvider.System,
            updatesPublisher);
        var liveSessionId = Guid.NewGuid();
        var sessionTeamId = Guid.NewGuid();
        var commandId = Guid.NewGuid();

        var response = await handler.Handle(
            new ApplyPenaltyCommand(
                liveSessionId,
                sessionTeamId,
                commandId,
                "Major",
                "operator-7",
                "Uso indebido de pista.",
                DateTimeOffset.Parse("2026-06-04T02:15:00Z")),
            CancellationToken.None);

        var persistedEvent = await dbContext.SessionEventLogs.SingleAsync();
        var expectedDescription = $"Penalty of severity 'Major' applied to Session Team '{sessionTeamId}' by Operator 'operator-7' for reason: Uso indebido de pista. Score variation: -100 points.";
        Assert.True(response.PenaltyApplied);
        Assert.Equal(liveSessionId, persistedEvent.LiveSessionId);
        Assert.Equal("PenaltyApplied", persistedEvent.EventType);
        Assert.Equal(expectedDescription, persistedEvent.Description);
        Assert.Single(updatesPublisher.EventLogPayloads);
        Assert.Equal(persistedEvent.Id, updatesPublisher.EventLogPayloads[0].Id);
        Assert.Single(updatesPublisher.RankingPayloads);
    }

    [Fact]
    public async Task ApplyPenaltyCommand_ReplayedCommandDoesNotDuplicateScoreEntryOrPenaltyAppliedEvent()
    {
        await using var dbContext = CreateDbContext();
        var updatesPublisher = new CapturingScoringMonitoringUpdatesPublisher();
        var handler = new ApplyPenaltyHandler(
            new ApplyPenaltyScoreboardStore(dbContext),
            TimeProvider.System,
            updatesPublisher);
        var liveSessionId = Guid.NewGuid();
        var sessionTeamId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var command = new ApplyPenaltyCommand(
            liveSessionId,
            sessionTeamId,
            commandId,
            "Minor",
            "operator-9",
            "Reenvio accidental.",
            DateTimeOffset.Parse("2026-06-04T02:45:00Z"));

        var firstResponse = await handler.Handle(command, CancellationToken.None);
        var replayResponse = await handler.Handle(command, CancellationToken.None);

        Assert.True(firstResponse.PenaltyApplied);
        Assert.False(replayResponse.PenaltyApplied);
        Assert.Equal(1, await dbContext.ScoreEntries.CountAsync());
        Assert.Equal(1, await dbContext.SessionEventLogs.CountAsync());
        var eventLog = await dbContext.SessionEventLogs.SingleAsync();
        Assert.Equal("PenaltyApplied", eventLog.EventType);
        Assert.Single(updatesPublisher.RankingPayloads);
        Assert.Single(updatesPublisher.EventLogPayloads);
    }

    private static ScoringMonitoringDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ScoringMonitoringDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ScoringMonitoringDbContext(options);
    }

    private sealed class CapturingScoringMonitoringUpdatesPublisher : IScoringMonitoringUpdatesPublisher
    {
        public List<RankingPayload> RankingPayloads { get; } = [];

        public List<SessionEventLogPayload> EventLogPayloads { get; } = [];

        public Task PublishRankingUpdatedAsync(RankingPayload ranking, CancellationToken cancellationToken)
        {
            RankingPayloads.Add(ranking);
            return Task.CompletedTask;
        }

        public Task PublishEventLogUpdatedAsync(
            SessionEventLogPayload eventLog,
            CancellationToken cancellationToken)
        {
            EventLogPayloads.Add(eventLog);
            return Task.CompletedTask;
        }
    }
}
