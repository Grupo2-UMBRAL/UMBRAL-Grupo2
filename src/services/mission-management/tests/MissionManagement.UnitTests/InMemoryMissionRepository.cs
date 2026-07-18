using MissionManagement.Application.Abstractions;
using MissionManagement.Domain.Missions;

namespace MissionManagement.UnitTests;

/// <summary>
/// Hand-written in-memory <see cref="IMissionRepository"/> for command-handler unit tests. Keeps the
/// seeded aggregates so mutations the handler applies (Activate/Deactivate/UpdateDetails) are
/// observable, and counts the mutators so tests can assert persistence did or did not happen.
/// </summary>
internal sealed class InMemoryMissionRepository : IMissionRepository
{
    private readonly Dictionary<Guid, Mission> _missions = new();

    public InMemoryMissionRepository(params Mission[] seed)
    {
        foreach (var mission in seed)
        {
            _missions[mission.Id] = mission;
        }
    }

    public int PersistCallCount { get; private set; }

    public IReadOnlyDictionary<Guid, Mission> Missions => _missions;

    public Task<Mission?> GetAsync(Guid missionId, CancellationToken cancellationToken)
        => Task.FromResult(_missions.TryGetValue(missionId, out var mission) ? mission : null);

    public Task<Mission?> GetWithItemsAsync(Guid missionId, CancellationToken cancellationToken)
        => Task.FromResult(_missions.TryGetValue(missionId, out var mission) ? mission : null);

    public Task<Mission> GetRequiredWithItemsAsync(Guid missionId, CancellationToken cancellationToken)
        => Task.FromResult(_missions[missionId]);

    public Task<IReadOnlyList<Mission>> ListMissionsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Mission>>(_missions.Values.ToList());

    public Task<IReadOnlyList<Mission>> ListEligibleMissionsAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Mission>>(_missions.Values.Where(m => m.IsActive).ToList());

    public Task AddAsync(Mission mission, CancellationToken cancellationToken)
    {
        _missions[mission.Id] = mission;
        PersistCallCount++;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Mission mission, CancellationToken cancellationToken)
    {
        PersistCallCount++;
        return Task.CompletedTask;
    }

    public Task ReplaceItemsAsync(Mission mission, CancellationToken cancellationToken)
    {
        _missions[mission.Id] = mission;
        PersistCallCount++;
        return Task.CompletedTask;
    }

    public Task<bool> NameExistsAsync(string name, Guid? excludeMissionId, CancellationToken cancellationToken)
        => Task.FromResult(_missions.Values.Any(mission =>
            mission.Name == name && mission.Id != excludeMissionId));
}
