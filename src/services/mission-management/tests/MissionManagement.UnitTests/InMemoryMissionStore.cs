using MissionManagement.Application.Abstractions;
using MissionManagement.Domain.Missions;

namespace MissionManagement.UnitTests;

/// <summary>
/// Hand-written in-memory <see cref="IMissionStore"/> for command-handler unit tests. Keeps the
/// seeded aggregates so mutations the handler applies (Activate/Deactivate/UpdateDetails) are
/// observable, and counts SaveChanges so tests can assert persistence did or did not happen.
/// </summary>
internal sealed class InMemoryMissionStore : IMissionStore
{
    private readonly Dictionary<Guid, Mission> _missions = new();

    public InMemoryMissionStore(params Mission[] seed)
    {
        foreach (var mission in seed)
        {
            _missions[mission.Id] = mission;
        }
    }

    public int SaveChangesCallCount { get; private set; }

    public IReadOnlyDictionary<Guid, Mission> Missions => _missions;

    public Task<Mission?> GetAsync(Guid missionId, CancellationToken cancellationToken)
        => Task.FromResult(_missions.TryGetValue(missionId, out var mission) ? mission : null);

    public Task<Mission?> GetWithItemsAsync(Guid missionId, CancellationToken cancellationToken)
        => Task.FromResult(_missions.TryGetValue(missionId, out var mission) ? mission : null);

    public void Add(Mission mission) => _missions[mission.Id] = mission;

    public Task ReplaceItemsAsync(Mission mission, CancellationToken cancellationToken)
    {
        _missions[mission.Id] = mission;
        return Task.CompletedTask;
    }

    public Task<bool> NameExistsAsync(string name, Guid? excludeMissionId, CancellationToken cancellationToken)
        => Task.FromResult(_missions.Values.Any(mission =>
            mission.Name == name && mission.Id != excludeMissionId));

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
