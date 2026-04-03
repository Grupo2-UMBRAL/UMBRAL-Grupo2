namespace Umbral.Domain.Missions.Repositories;

using Umbral.Domain.Missions.Entities;

public interface IMissionRepository
{
    Task<Mission?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Mission?> GetByIdWithStagesAsync(Guid id, CancellationToken ct = default);
    Task<List<Mission>> GetAllAsync(CancellationToken ct = default);
    Task<List<Mission>> GetActiveAsync(CancellationToken ct = default);
    Task AddAsync(Mission mission, CancellationToken ct = default);
    Task UpdateAsync(Mission mission, CancellationToken ct = default);
    Task AddStageAsync(MissionStage stage, CancellationToken ct = default);
    Task AddClueAsync(Clue clue, CancellationToken ct = default);
    Task<List<MissionStage>> GetStagesByMissionIdAsync(Guid missionId, CancellationToken ct = default);
    Task<List<Clue>> GetCluesByStageIdAsync(Guid stageId, CancellationToken ct = default);
}
