namespace Umbral.Domain.Scoring.Repositories;

using Umbral.Domain.Scoring.Entities;

public interface IScoreEntryRepository
{
    Task AddAsync(ScoreEntry entry, CancellationToken ct = default);
    Task<List<ScoreEntry>> GetByTeamAndSessionAsync(Guid teamId, Guid sessionId, CancellationToken ct = default);
    Task<int> GetTotalScoreAsync(Guid teamId, Guid sessionId, CancellationToken ct = default);
}
