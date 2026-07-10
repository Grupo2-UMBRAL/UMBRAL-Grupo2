using MissionManagement.Application.Abstractions;
using MissionManagement.Domain.Missions;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Features.Missions;

/// <summary>
/// Application-layer validation shared by the create and update use cases: a mission name must be
/// unique across all missions. This check lives here (not in the <see cref="Mission"/> aggregate)
/// because uniqueness spans the whole set, which a single aggregate cannot see, and it needs to query
/// the store. Both handlers call it so "duplicate name" has one definition instead of two inline copies.
/// </summary>
public static class MissionNameUniquenessValidator
{
    public static async Task EnsureAvailableAsync(
        IMissionStore missions,
        string name,
        Guid? excludeMissionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(missions);

        if (await missions.NameExistsAsync(name, excludeMissionId, cancellationToken))
        {
            throw new UmbralDomainException(
                "mission_name_duplicate",
                $"Mission '{name}' already exists.",
                UmbralFailureCategory.Conflict);
        }
    }
}
