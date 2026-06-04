using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Umbral.ScoringAudit.Api.Application.Audit;
using Umbral.ScoringAudit.Api.Application.Rankings;
using Umbral.ScoringAudit.Api.Application.Scoreboards;
using Umbral.ScoringAudit.Api.Domain.Audit;
using Umbral.ScoringAudit.Api.Hubs;
using Umbral.ScoringAudit.Api.Hubs.Contracts;
using Umbral.ScoringAudit.Api.Infrastructure;
using Xunit;

namespace Umbral.ScoringAudit.Api.Tests;

public sealed class SessionEventLogApplicationTests
{
    [Fact]
    public async Task LogSessionEventCommand_PersistsEventLogAndPublishesRealtimePayload()
    {
        await using var dbContext = CreateDbContext();
        var hubClient = new CapturingScoringAuditClient();
        var handler = new LogSessionEventHandler(
            dbContext,
            TimeProvider.System,
            new CapturingScoringAuditHubContext(hubClient));
        var liveSessionId = Guid.NewGuid();

        var payload = await handler.Handle(
            new LogSessionEventCommand(liveSessionId, "OperatorNote", "Operador marco control manual."),
            CancellationToken.None);

        var persisted = await dbContext.SessionEventLogs.SingleAsync();
        Assert.Equal(payload.Id, persisted.Id);
        Assert.Equal(liveSessionId, persisted.LiveSessionId);
        Assert.Equal("OperatorNote", persisted.EventType);
        Assert.Equal("Operador marco control manual.", persisted.Description);
        Assert.Single(hubClient.EventLogPayloads);
        Assert.Equal(payload, hubClient.EventLogPayloads[0]);
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
        var handler = new GetSessionEventLogHandler(dbContext);

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
        var handler = new GetSessionEventLogHandler(dbContext);

        var payload = await handler.Handle(new GetSessionEventLogQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Empty(payload);
    }

    [Fact]
    public async Task RecordStageCreditCommand_PersistsStageCreditAuditEvent()
    {
        await using var dbContext = CreateDbContext();
        var hubClient = new CapturingScoringAuditClient();
        var handler = new RecordStageCreditHandler(
            dbContext,
            TimeProvider.System,
            new CapturingScoringAuditHubContext(hubClient));
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
        Assert.Single(hubClient.EventLogPayloads);
        Assert.Equal(persistedEvent.Id, hubClient.EventLogPayloads[0].Id);
    }

    private static ScoringAuditDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ScoringAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ScoringAuditDbContext(options);
    }

    private sealed class CapturingScoringAuditHubContext(CapturingScoringAuditClient client)
        : IHubContext<ScoringAuditHub, IScoringAuditClient>
    {
        public IHubClients<IScoringAuditClient> Clients { get; } = new CapturingHubClients(client);

        public IGroupManager Groups { get; } = new NoOpGroupManager();
    }

    private sealed class CapturingHubClients(IScoringAuditClient client) : IHubClients<IScoringAuditClient>
    {
        public IScoringAuditClient All => client;

        public IScoringAuditClient AllExcept(IReadOnlyList<string> excludedConnectionIds) => client;

        public IScoringAuditClient Client(string connectionId) => client;

        public IScoringAuditClient Clients(IReadOnlyList<string> connectionIds) => client;

        public IScoringAuditClient Group(string groupName) => client;

        public IScoringAuditClient GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => client;

        public IScoringAuditClient Groups(IReadOnlyList<string> groupNames) => client;

        public IScoringAuditClient User(string userId) => client;

        public IScoringAuditClient Users(IReadOnlyList<string> userIds) => client;
    }

    private sealed class NoOpGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class CapturingScoringAuditClient : IScoringAuditClient
    {
        public List<RankingPayload> RankingPayloads { get; } = [];

        public List<SessionEventLogPayload> EventLogPayloads { get; } = [];

        public Task ReceiveRankingUpdated(RankingPayload payload)
        {
            RankingPayloads.Add(payload);
            return Task.CompletedTask;
        }

        public Task ReceiveEventLogUpdated(SessionEventLogPayload payload)
        {
            EventLogPayloads.Add(payload);
            return Task.CompletedTask;
        }
    }
}
