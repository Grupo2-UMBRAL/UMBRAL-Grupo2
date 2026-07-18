using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.Application.Abstractions;

public interface ILiveSessionReadRepository
{
    Task<LiveSession?> GetByIdWithSessionTeamsAsync(Guid liveSessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LiveSession>> ListWithSessionTeamsAsync(CancellationToken cancellationToken = default);
    Task<LiveSession?> GetByJoinCodeAsync(string joinCodeValue, CancellationToken cancellationToken = default);
    Task<LiveSession?> GetByJoinCodeWithSessionTeamsAsync(string joinCodeValue, CancellationToken cancellationToken = default);
    Task<LiveSession?> GetOverviewAsync(Guid liveSessionId, CancellationToken cancellationToken = default);
    Task<LiveSession?> GetSessionTeamSnapshotAsync(Guid sessionTeamId, CancellationToken cancellationToken = default);
    Task<LiveSession?> GetSessionTeamDetailAsync(Guid liveSessionId, Guid sessionTeamId, CancellationToken cancellationToken = default);
}
