using Microsoft.EntityFrameworkCore;
using MissionManagement.Application.Abstractions;
using MissionManagement.Application.Features.Missions;
using MissionManagement.Domain.Missions;

namespace MissionManagement.Infrastructure.Persistence;

/// <summary>
/// Command-side implementation of <see cref="IMissionStore"/>. The scalar row is loaded tracked so
/// domain mutations persist on <see cref="SaveChangesAsync"/>; the path-item tree is rebuilt from the
/// flat rows via <see cref="MissionLoader"/> and attached in memory (the Mission has no EF navigation
/// to its items, so a hydrated tree never dirties the change tracker on its own).
/// </summary>
internal sealed class MissionStore(IMissionManagementDbContext dbContext) : IMissionStore
{
    public async Task<Mission?> GetAsync(Guid missionId, CancellationToken cancellationToken)
    {
        return await dbContext.Missions
            .SingleOrDefaultAsync(mission => mission.Id == missionId, cancellationToken);
    }

    public async Task<Mission?> GetWithItemsAsync(Guid missionId, CancellationToken cancellationToken)
    {
        var mission = await dbContext.Missions
            .SingleOrDefaultAsync(existing => existing.Id == missionId, cancellationToken);
        if (mission is null)
        {
            return null;
        }

        // The tracked scalar row carries no path items (no EF navigation); hydrate the tree from the
        // flat rows and graft it onto the tracked aggregate for eligibility checks and the response.
        var hydrated = await MissionLoader.LoadAsync(dbContext, missionId, cancellationToken);
        mission.ReplaceItems(hydrated!.RootItems);
        return mission;
    }

    public void Add(Mission mission)
    {
        ArgumentNullException.ThrowIfNull(mission);

        dbContext.Missions.Add(mission);
        MissionLoader.AddItems(dbContext, mission);
    }

    public async Task ReplaceItemsAsync(Mission mission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mission);

        await MissionLoader.DeleteItemsAsync(dbContext, mission.Id, cancellationToken);
        MissionLoader.AddItems(dbContext, mission);
    }

    public async Task<bool> NameExistsAsync(string name, Guid? excludeMissionId, CancellationToken cancellationToken)
    {
        // Update passes its own id so renaming a mission to its current name is not "taken by another".
        return excludeMissionId is { } id
            ? await dbContext.Missions.AsNoTracking()
                .AnyAsync(mission => mission.Id != id && mission.Name == name, cancellationToken)
            : await dbContext.Missions.AsNoTracking()
                .AnyAsync(mission => mission.Name == name, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
