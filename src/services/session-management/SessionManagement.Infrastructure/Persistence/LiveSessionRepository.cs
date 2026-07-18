using Microsoft.EntityFrameworkCore;
using SessionManagement.Application.Abstractions;
using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.Infrastructure.Persistence;

internal sealed class LiveSessionRepository : ILiveSessionRepository
{
    private readonly SessionManagementDbContext _dbContext;

    public LiveSessionRepository(SessionManagementDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LiveSession?> GetAsync(Guid liveSessionId, CancellationToken cancellationToken = default)
        => await _dbContext.LiveSessions.SingleOrDefaultAsync(session => session.Id == liveSessionId, cancellationToken);

    public async Task<LiveSession?> GetWithSessionTeamsAsync(Guid liveSessionId, CancellationToken cancellationToken = default)
        => await _dbContext.LiveSessions.Include(session => session.SessionTeams)
            .SingleOrDefaultAsync(session => session.Id == liveSessionId, cancellationToken);

    public async Task<LiveSession?> GetByJoinCodeWithEnrollmentAsync(string joinCodeValue, CancellationToken cancellationToken = default)
        => await _dbContext.LiveSessions.Include(session => session.SessionTeams).Include(session => session.TeamParticipations)
            .SingleOrDefaultAsync(session => session.JoinCodeValue == joinCodeValue, cancellationToken);

    public async Task<LiveSession?> GetForHintReleaseAsync(Guid liveSessionId, CancellationToken cancellationToken = default)
        => await _dbContext.LiveSessions.Include(session => session.SessionTeams).Include(session => session.TeamProgressions)
            .Include(session => session.ReleasedHints).SingleOrDefaultAsync(session => session.Id == liveSessionId, cancellationToken);

    public async Task<LiveSession?> GetForLifecycleTransitionAsync(Guid liveSessionId, CancellationToken cancellationToken = default)
        => await _dbContext.LiveSessions.Include(session => session.SessionTeams).Include(session => session.ReleasedHints)
            .SingleOrDefaultAsync(session => session.Id == liveSessionId, cancellationToken);

    public async Task<LiveSession?> GetForSessionFlowDeactivationAsync(Guid liveSessionId, CancellationToken cancellationToken = default)
        => await _dbContext.LiveSessions.Include(session => session.SessionTeams).Include(session => session.TeamProgressions)
            .SingleOrDefaultAsync(session => session.Id == liveSessionId, cancellationToken);

    public Task AddAsync(LiveSession liveSession, CancellationToken cancellationToken = default)
    {
        _dbContext.LiveSessions.Add(liveSession);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _dbContext.SaveChangesAsync(cancellationToken);

    public async Task<LiveSession?> GetBySessionTeamIdWithEvidenceSubmissionsAsync(Guid sessionTeamId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<LiveSession>()
            .Include(session => session.SessionTeams)
            .Include(session => session.TeamParticipations)
            .Include(session => session.TeamProgressions)
            .Include(session => session.EvidenceSubmissions)
            .SingleOrDefaultAsync(
                session => session.SessionTeams.Any(team => team.Id == sessionTeamId),
                cancellationToken);
    }

    public async Task<LiveSession?> GetByEvidenceSubmissionIdWithSubmissionsAndLogsAsync(Guid evidenceSubmissionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<LiveSession>()
            .Include(session => session.SessionTeams)
            .Include(session => session.TeamProgressions)
            .Include(session => session.EvidenceSubmissions)
            .Include(session => session.ValidationOverrideLogs)
            .SingleOrDefaultAsync(
                session => session.EvidenceSubmissions.Any(submission => submission.Id == evidenceSubmissionId),
                cancellationToken);
    }
}
