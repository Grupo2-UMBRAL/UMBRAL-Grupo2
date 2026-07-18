using MissionManagement.Application.Features.Missions;
using MissionManagement.Domain.Missions;
using Umbral.ServiceDefaults;
using Xunit;

namespace MissionManagement.UnitTests;

public sealed class MissionNameUniquenessValidatorTests
{
    [Fact]
    public async Task EnsureAvailableAsync_NoClash_DoesNotThrow()
    {
        var store = new InMemoryMissionRepository();

        await MissionNameUniquenessValidator.EnsureAvailableAsync(
            store, "Unique", excludeMissionId: null, CancellationToken.None);
    }

    [Fact]
    public async Task EnsureAvailableAsync_Clash_ThrowsConflict()
    {
        var store = new InMemoryMissionRepository(Mission.Create(Guid.NewGuid(), "Taken", "Description", 30));

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            MissionNameUniquenessValidator.EnsureAvailableAsync(
                store, "Taken", excludeMissionId: Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("mission_name_duplicate", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
    }

    [Fact]
    public async Task EnsureAvailableAsync_SameMissionExcluded_DoesNotThrow()
    {
        var mission = Mission.Create(Guid.NewGuid(), "Keep Name", "Description", 30);
        var store = new InMemoryMissionRepository(mission);

        await MissionNameUniquenessValidator.EnsureAvailableAsync(
            store, "Keep Name", excludeMissionId: mission.Id, CancellationToken.None);
    }
}
