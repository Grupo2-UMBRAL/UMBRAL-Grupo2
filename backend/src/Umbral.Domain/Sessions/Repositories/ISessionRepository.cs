namespace Umbral.Domain.Sessions.Repositories;

using Umbral.Domain.Sessions.Entities;

public interface ISessionRepository
{
    Task<LiveSession?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<LiveSession?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<List<LiveSession>> GetByOperatorIdAsync(Guid operatorId, CancellationToken ct = default);
    Task AddAsync(LiveSession session, CancellationToken ct = default);
    Task UpdateAsync(LiveSession session, CancellationToken ct = default);
    Task AddEventAsync(SessionEvent sessionEvent, CancellationToken ct = default);
    Task<List<SessionEvent>> GetEventsBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
}
