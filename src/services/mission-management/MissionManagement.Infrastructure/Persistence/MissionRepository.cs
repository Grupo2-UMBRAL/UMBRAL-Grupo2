using Microsoft.EntityFrameworkCore;
using MissionManagement.Application.Abstractions;
using MissionManagement.Domain.Missions;

namespace MissionManagement.Infrastructure.Persistence;


internal sealed class MissionRepository(MissionManagementDbContext dbContext) : IMissionRepository
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

    public async Task<Mission> GetRequiredWithItemsAsync(Guid missionId, CancellationToken cancellationToken)
    {
        return await MissionLoader.RequireAsync(dbContext, missionId, cancellationToken);
    }

    public async Task<IReadOnlyList<Mission>> ListMissionsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Missions
            .AsNoTracking()
            .OrderBy(mission => mission.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Mission>> ListEligibleMissionsAsync(CancellationToken cancellationToken)
    {
        var activeMissionIds = await dbContext.Missions
            .AsNoTracking()
            .Where(mission => mission.IsActive)
            .OrderBy(mission => mission.Name)
            .Select(mission => mission.Id)
            .ToListAsync(cancellationToken);

        var eligibleMissions = new List<Mission>();
        foreach (var missionId in activeMissionIds)
        {
            var mission = await MissionLoader.LoadAsync(dbContext, missionId, cancellationToken);
            if (mission is null || !mission.IsEligibleForLiveSession())
            {
                continue;
            }

            eligibleMissions.Add(mission);
        }

        return eligibleMissions;
    }

    public async Task AddAsync(Mission mission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mission);

        dbContext.Missions.Add(mission);
        MissionLoader.AddItems(dbContext, mission);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Mission mission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mission);

        // The aggregate came back tracked from a get flavor, so its mutations are already staged.
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceItemsAsync(Mission mission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mission);

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);

        // The removed rows must reach the database before the replacements are staged: a request that
        // reuses the stored ids would otherwise collide with the still-tracked Deleted entries.
        await MissionLoader.DeleteItemsAsync(dbContext, mission.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        MissionLoader.AddItems(dbContext, mission);
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
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
}
