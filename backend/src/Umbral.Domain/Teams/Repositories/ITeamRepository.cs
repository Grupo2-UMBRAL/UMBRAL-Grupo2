namespace Umbral.Domain.Teams.Repositories;

using Umbral.Domain.Teams.Entities;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Team?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<List<Team>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<int> CountBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
    Task AddAsync(Team team, CancellationToken ct = default);
    Task UpdateAsync(Team team, CancellationToken ct = default);
    Task AddClueReleaseAsync(TeamClueRelease release, CancellationToken ct = default);
    Task<bool> IsClueReleasedAsync(Guid clueId, Guid teamId, CancellationToken ct = default);
    Task<List<TeamClueRelease>> GetReleasedCluesAsync(Guid teamId, Guid sessionId, CancellationToken ct = default);
    Task AddStageProgressAsync(TeamStageProgress progress, CancellationToken ct = default);
    Task<TeamStageProgress?> GetStageProgressAsync(Guid stageId, Guid teamId, Guid sessionId, CancellationToken ct = default);
    Task UpdateStageProgressAsync(TeamStageProgress progress, CancellationToken ct = default);
}
