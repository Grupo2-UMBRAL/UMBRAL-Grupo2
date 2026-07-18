using SessionManagement.Application.Abstractions;
using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.UnitTests;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class FakeLiveSessionRepository(IEnumerable<LiveSession> items) : ILiveSessionRepository
{
    private readonly List<LiveSession> _items = items.ToList();
    public int SaveChangesCount { get; private set; }
    public Task<LiveSession?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_items.SingleOrDefault(s => s.Id == id));
    public Task<LiveSession?> GetWithSessionTeamsAsync(Guid id, CancellationToken cancellationToken = default) => GetAsync(id, cancellationToken);
    public Task<LiveSession?> GetByJoinCodeWithEnrollmentAsync(string code, CancellationToken cancellationToken = default) => Task.FromResult(_items.SingleOrDefault(s => s.JoinCodeValue == code));
    public Task<LiveSession?> GetForHintReleaseAsync(Guid id, CancellationToken cancellationToken = default) => GetAsync(id, cancellationToken);
    public Task<LiveSession?> GetForLifecycleTransitionAsync(Guid id, CancellationToken cancellationToken = default) => GetAsync(id, cancellationToken);
    public Task<LiveSession?> GetForSessionFlowDeactivationAsync(Guid id, CancellationToken cancellationToken = default) => GetAsync(id, cancellationToken);
    public Task<LiveSession?> GetBySessionTeamIdWithEvidenceSubmissionsAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_items.SingleOrDefault(s => s.SessionTeams.Any(t => t.Id == id)));
    public Task<LiveSession?> GetByEvidenceSubmissionIdWithSubmissionsAndLogsAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_items.SingleOrDefault(s => s.EvidenceSubmissions.Any(e => e.Id == id)));
    public Task AddAsync(LiveSession session, CancellationToken cancellationToken = default) { _items.Add(session); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) { SaveChangesCount++; return Task.CompletedTask; }
}

internal sealed class FakeLiveSessionReadRepository(IEnumerable<LiveSession> items) : ILiveSessionReadRepository
{
    private readonly List<LiveSession> _items = items.ToList();
    public Task<LiveSession?> GetByIdWithSessionTeamsAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_items.SingleOrDefault(s => s.Id == id));
    public Task<IReadOnlyList<LiveSession>> ListWithSessionTeamsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LiveSession>>(_items.OrderByDescending(s => s.CreatedAtUtc).ToArray());
    public Task<LiveSession?> GetByJoinCodeAsync(string code, CancellationToken cancellationToken = default) => Task.FromResult(_items.SingleOrDefault(s => s.JoinCodeValue == code));
    public Task<LiveSession?> GetByJoinCodeWithSessionTeamsAsync(string code, CancellationToken cancellationToken = default) => GetByJoinCodeAsync(code, cancellationToken);
    public Task<LiveSession?> GetOverviewAsync(Guid id, CancellationToken cancellationToken = default) => GetByIdWithSessionTeamsAsync(id, cancellationToken);
    public Task<LiveSession?> GetSessionTeamSnapshotAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_items.SingleOrDefault(s => s.SessionTeams.Any(t => t.Id == id)));
    public Task<LiveSession?> GetSessionTeamDetailAsync(Guid id, Guid team, CancellationToken cancellationToken = default) => Task.FromResult(_items.SingleOrDefault(s => s.Id == id && s.SessionTeams.Any(t => t.Id == team)));
}
