using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.Application.Abstractions;

public interface ILiveSessionRepository
{
    Task<LiveSession?> GetAsync(Guid liveSessionId, CancellationToken cancellationToken = default);
    Task<LiveSession?> GetWithSessionTeamsAsync(Guid liveSessionId, CancellationToken cancellationToken = default);
    Task<LiveSession?> GetByJoinCodeWithEnrollmentAsync(string joinCodeValue, CancellationToken cancellationToken = default);
    Task<LiveSession?> GetForHintReleaseAsync(Guid liveSessionId, CancellationToken cancellationToken = default);
    Task<LiveSession?> GetForLifecycleTransitionAsync(Guid liveSessionId, CancellationToken cancellationToken = default);
    Task<LiveSession?> GetForSessionFlowDeactivationAsync(Guid liveSessionId, CancellationToken cancellationToken = default);
    Task<LiveSession?> GetBySessionTeamIdWithEvidenceSubmissionsAsync(Guid sessionTeamId, CancellationToken cancellationToken = default);
    Task<LiveSession?> GetByEvidenceSubmissionIdWithSubmissionsAndLogsAsync(Guid evidenceSubmissionId, CancellationToken cancellationToken = default);
    Task AddAsync(LiveSession liveSession, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
