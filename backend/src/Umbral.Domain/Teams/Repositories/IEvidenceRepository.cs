namespace Umbral.Domain.Teams.Repositories;

using Umbral.Domain.Teams.Entities;

public interface IEvidenceRepository
{
    Task<Evidence?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Evidence>> GetByTeamAndSessionAsync(Guid teamId, Guid sessionId, CancellationToken ct = default);
    Task<Evidence?> GetByTeamStageAndSessionAsync(Guid teamId, Guid stageId, Guid sessionId, CancellationToken ct = default);
    Task AddAsync(Evidence evidence, CancellationToken ct = default);
    Task UpdateAsync(Evidence evidence, CancellationToken ct = default);
}
