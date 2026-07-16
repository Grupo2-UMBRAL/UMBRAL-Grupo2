using MissionManagement.Domain.Missions;

namespace MissionManagement.Application.Abstractions;

/// <summary>
/// Command-side persistence seam for the <see cref="Mission"/> aggregate. Purpose-built loads keep EF
/// out of the command handlers (they mock this), and the two get flavors let a handler fetch only what
/// it needs: the scalar row when a state flag is all that changes, the full path-item tree when
/// eligibility or the response body needs it.
/// </summary>
public interface IMissionStore
{
    /// <summary>Tracked scalar aggregate (no path-item tree). For mutations that only touch scalar state.</summary>
    Task<Mission?> GetAsync(Guid missionId, CancellationToken cancellationToken);

    /// <summary>Tracked scalar aggregate with its path-item tree hydrated (for eligibility / full response).</summary>
    Task<Mission?> GetWithItemsAsync(Guid missionId, CancellationToken cancellationToken);

    /// <summary>Stages a new mission (scalar row + its path-item tree) for insertion.</summary>
    void Add(Mission mission);

    /// <summary>Atomically replaces and persists the stored path-item tree of a mission with the one carried by <paramref name="mission"/>.</summary>
    Task ReplaceItemsAsync(Mission mission, CancellationToken cancellationToken);

    /// <summary>True when another mission already uses <paramref name="name"/> (excluding <paramref name="excludeMissionId"/>).</summary>
    Task<bool> NameExistsAsync(string name, Guid? excludeMissionId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
