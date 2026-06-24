using MediatR;
using Microsoft.EntityFrameworkCore;
using MissionManagement.Application.Abstractions;
using MissionManagement.Domain.Missions;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Features.Missions;

/// <summary>
/// Loads and persists the Mission aggregate. The aggregate has no arbitrary-depth navigation, so we
/// fetch the flat persistence rows (path items + plays + choices + hints) and let
/// <see cref="Mission.Rehydrate"/> rebuild the Section tree in memory. Replacement deletes existing
/// path items (cascade clears plays/choices/hints) before attaching the new tree.
/// </summary>
public static class MissionLoader
{
    public static async Task<Mission?> LoadAsync(
        IMissionManagementDbContext dbContext,
        Guid missionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var missionRow = await dbContext.Missions
            .AsNoTracking()
            .SingleOrDefaultAsync(mission => mission.Id == missionId, cancellationToken);
        if (missionRow is null)
        {
            return null;
        }

        var sections = await dbContext.Sections
            .AsNoTracking()
            .Where(section => section.MissionId == missionId)
            .ToListAsync(cancellationToken);

        var challenges = await dbContext.Challenges
            .AsNoTracking()
            .Where(challenge => challenge.MissionId == missionId)
            .ToListAsync(cancellationToken);

        var plays = new List<Play>();
        var choices = new List<Choice>();
        var hints = new List<Hint>();

        if (challenges.Count > 0)
        {
            var challengeIds = challenges.Select(challenge => challenge.Id).ToHashSet();

            plays = await dbContext.Plays
                .AsNoTracking()
                .Where(play => challengeIds.Contains(play.ChallengeId))
                .ToListAsync(cancellationToken);

            var questionIds = plays.OfType<Question>().Select(question => question.Id).ToHashSet();
            var searchIds = plays.OfType<Search>().Select(search => search.Id).ToHashSet();

            if (questionIds.Count > 0)
            {
                choices = await dbContext.Choices
                    .AsNoTracking()
                    .Where(choice => questionIds.Contains(choice.QuestionId))
                    .ToListAsync(cancellationToken);
            }

            if (searchIds.Count > 0)
            {
                hints = await dbContext.Hints
                    .AsNoTracking()
                    .Where(hint => searchIds.Contains(hint.SearchId))
                    .ToListAsync(cancellationToken);
            }
        }

        return Mission.Rehydrate(
            missionRow.Id,
            missionRow.Name,
            missionRow.Description,
            missionRow.MaximumDurationMinutes,
            missionRow.IsActive,
            sections,
            challenges,
            plays,
            choices,
            hints);
    }

    public static async Task<Mission> RequireAsync(
        IMissionManagementDbContext dbContext,
        Guid missionId,
        CancellationToken cancellationToken)
    {
        var mission = await LoadAsync(dbContext, missionId, cancellationToken);
        if (mission is null)
        {
            throw new UmbralDomainException(
                "mission_not_found",
                $"Mission '{missionId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        return mission;
    }

    /// <summary>Removes all path items (and cascaded plays/choices/hints) currently stored for a mission.</summary>
    public static async Task DeleteItemsAsync(
        IMissionManagementDbContext dbContext,
        Guid missionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var existingItems = await dbContext.PathItems
            .Where(item => item.MissionId == missionId)
            .ToListAsync(cancellationToken);

        dbContext.PathItems.RemoveRange(existingItems);
    }

    /// <summary>Adds the mission's root path items (recursively, depth-first) to the change tracker.</summary>
    public static void AddItems(IMissionManagementDbContext dbContext, Mission mission)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(mission);

        foreach (var item in EnumerateDepthFirst(mission.RootItems))
        {
            dbContext.PathItems.Add(item);
        }
    }

    private static IEnumerable<PathItem> EnumerateDepthFirst(IReadOnlyList<PathItem> items)
    {
        foreach (var item in items)
        {
            yield return item;

            if (item is Section section)
            {
                foreach (var child in EnumerateDepthFirst(section.Children))
                {
                    yield return child;
                }
            }
        }
    }
}
