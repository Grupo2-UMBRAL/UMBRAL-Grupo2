using System.Collections;
using System.Linq.Expressions;
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

    public Type ElementType => ((IQueryable<LiveSession>)_dbContext.Set<LiveSession>()).ElementType;
    public Expression Expression => ((IQueryable<LiveSession>)_dbContext.Set<LiveSession>()).Expression;
    public IQueryProvider Provider => ((IQueryable<LiveSession>)_dbContext.Set<LiveSession>()).Provider;
    
    public IEnumerator<LiveSession> GetEnumerator() => ((IQueryable<LiveSession>)_dbContext.Set<LiveSession>()).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_dbContext.Set<LiveSession>()).GetEnumerator();

    public async Task<LiveSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => 
        await _dbContext.Set<LiveSession>().FindAsync(new object[] { id }, cancellationToken);

    public async Task<IReadOnlyList<LiveSession>> GetAllAsync(CancellationToken cancellationToken = default) => 
        await _dbContext.Set<LiveSession>().ToListAsync(cancellationToken);

    public async Task<LiveSession?> SingleOrDefaultAsync(Expression<Func<LiveSession, bool>> predicate, CancellationToken cancellationToken = default) => 
        await _dbContext.Set<LiveSession>().SingleOrDefaultAsync(predicate, cancellationToken);

    public async Task<LiveSession?> FirstOrDefaultAsync(Expression<Func<LiveSession, bool>> predicate, CancellationToken cancellationToken = default) => 
        await _dbContext.Set<LiveSession>().FirstOrDefaultAsync(predicate, cancellationToken);

    public async Task<IReadOnlyList<LiveSession>> GetAsync(Expression<Func<LiveSession, bool>> predicate, CancellationToken cancellationToken = default) => 
        await _dbContext.Set<LiveSession>().Where(predicate).ToListAsync(cancellationToken);

    public void Add(LiveSession entity) => _dbContext.Set<LiveSession>().Add(entity);
    public void Update(LiveSession entity) => _dbContext.Set<LiveSession>().Update(entity);
    public void Remove(LiveSession entity) => _dbContext.Set<LiveSession>().Remove(entity);

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
