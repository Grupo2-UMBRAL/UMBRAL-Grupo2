using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.Application.Abstractions;

public interface ILiveSessionRepository : IRepository<LiveSession>
{
    Task<LiveSession?> GetBySessionTeamIdWithEvidenceSubmissionsAsync(Guid sessionTeamId, CancellationToken cancellationToken = default);
    Task<LiveSession?> GetByEvidenceSubmissionIdWithSubmissionsAndLogsAsync(Guid evidenceSubmissionId, CancellationToken cancellationToken = default);
}
