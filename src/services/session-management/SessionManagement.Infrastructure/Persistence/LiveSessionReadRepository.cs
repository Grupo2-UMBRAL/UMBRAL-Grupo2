using Microsoft.EntityFrameworkCore;
using SessionManagement.Application.Abstractions;
using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.Infrastructure.Persistence;

internal sealed class LiveSessionReadRepository(SessionManagementDbContext dbContext) : ILiveSessionReadRepository
{
    public Task<LiveSession?> GetByIdWithSessionTeamsAsync(Guid id, CancellationToken cancellationToken = default) => dbContext.LiveSessions.AsNoTracking().Include(s => s.SessionTeams).SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
    public async Task<IReadOnlyList<LiveSession>> ListWithSessionTeamsAsync(CancellationToken cancellationToken = default) => await dbContext.LiveSessions.AsNoTracking().Include(s => s.SessionTeams).OrderByDescending(s => s.CreatedAtUtc).ToListAsync(cancellationToken);
    public Task<LiveSession?> GetByJoinCodeAsync(string code, CancellationToken cancellationToken = default) => dbContext.LiveSessions.AsNoTracking().SingleOrDefaultAsync(s => s.JoinCodeValue == code, cancellationToken);
    public Task<LiveSession?> GetByJoinCodeWithSessionTeamsAsync(string code, CancellationToken cancellationToken = default) => dbContext.LiveSessions.AsNoTracking().Include(s => s.SessionTeams).SingleOrDefaultAsync(s => s.JoinCodeValue == code, cancellationToken);
    public Task<LiveSession?> GetOverviewAsync(Guid id, CancellationToken cancellationToken = default) => dbContext.LiveSessions.AsNoTracking().Include(s => s.SessionTeams).Include(s => s.TeamParticipations).Include(s => s.TeamProgressions).Include(s => s.EvidenceSubmissions).Include(s => s.ReleasedHints).SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
    public Task<LiveSession?> GetSessionTeamSnapshotAsync(Guid teamId, CancellationToken cancellationToken = default) => dbContext.LiveSessions.AsNoTracking().Include(s => s.SessionTeams).Include(s => s.TeamParticipations).Include(s => s.TeamProgressions).Include(s => s.EvidenceSubmissions).Include(s => s.ReleasedHints).SingleOrDefaultAsync(s => s.SessionTeams.Any(t => t.Id == teamId), cancellationToken);
    public Task<LiveSession?> GetSessionTeamDetailAsync(Guid id, Guid teamId, CancellationToken cancellationToken = default) => dbContext.LiveSessions.AsNoTracking().AsSplitQuery().Include(s => s.SessionTeams.Where(t => t.Id == teamId)).Include(s => s.TeamParticipations.Where(p => p.SessionTeamId == teamId)).Include(s => s.TeamProgressions.Where(p => p.SessionTeamId == teamId)).Include(s => s.EvidenceSubmissions.Where(e => e.SessionTeamId == teamId)).Include(s => s.ReleasedHints.Where(h => h.SessionTeamId == teamId)).SingleOrDefaultAsync(s => s.Id == id && s.SessionTeams.Any(t => t.Id == teamId), cancellationToken);
}
