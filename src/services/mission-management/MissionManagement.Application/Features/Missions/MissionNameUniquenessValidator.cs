using MissionManagement.Application.Abstractions;
using MissionManagement.Domain.Missions;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Features.Missions;

/// <summary>
/// Application-layer validation shared by the create and update use cases: a mission name must be
/// unique across all missions. This check lives here (not in the <see cref="Mission"/> aggregate)
/// because uniqueness spans the whole set, which a single aggregate cannot see, and it needs to query
/// the repository. Both handlers call it so "duplicate name" has one definition instead of two inline copies.
/// </summary>
public static class MissionNameUniquenessValidator
{
    public static async Task EnsureAvailableAsync(
        IRepository<Mission> missions,
        string name,
        Guid? excludeMissionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(missions);

        // Update passes its own id so renaming a mission to its current name is not "taken by another".
        var clash = excludeMissionId is { } id
            ? await missions.FirstOrDefaultAsync(mission => mission.Id != id && mission.Name == name, cancellationToken)
            : await missions.FirstOrDefaultAsync(mission => mission.Name == name, cancellationToken);

        if (clash is not null)
        {
            throw new UmbralDomainException(
                "mission_name_duplicate",
                $"Mission '{name}' already exists.",
                UmbralFailureCategory.Conflict);
        }
    }
}
