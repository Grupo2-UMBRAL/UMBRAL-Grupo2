using MissionManagement.Application.Features.Missions.Commands.DeactivateMission;
using MissionManagement.Domain.Missions;
using Umbral.ServiceDefaults;
using Xunit;

namespace MissionManagement.UnitTests;

public sealed class DeactivateMissionCommandHandlerTests
{
    [Fact]
    public async Task Handle_RejectsNullRequest()
    {
        var handler = new DeactivateMissionCommandHandler(new InMemoryMissionStore());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenMissionMissing()
    {
        var store = new InMemoryMissionStore();
        var handler = new DeactivateMissionCommandHandler(store);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(new DeactivateMissionCommand(Guid.NewGuid()), CancellationToken.None));

        Assert.Equal("mission_not_found", exception.Code);
        Assert.Equal(0, store.PersistCallCount);
    }

    [Fact]
    public async Task Handle_DeactivatesActiveMission_AndKeepsItemsInResponse()
    {
        var mission = SampleMissions.SingleTreasureHunt(Guid.NewGuid(), "Deactivation Mission");
        mission.Activate();
        var store = new InMemoryMissionStore(mission);
        var handler = new DeactivateMissionCommandHandler(store);

        var response = await handler.Handle(new DeactivateMissionCommand(mission.Id), CancellationToken.None);

        Assert.False(response.IsActive);
        // Response body still carries the mission tree (the web admin re-renders the builder from it).
        Assert.NotEmpty(response.Items);
        Assert.Equal(1, store.PersistCallCount);
    }

    [Fact]
    public async Task Handle_ThrowsConflict_WhenAlreadyInactive()
    {
        var mission = SampleMissions.SingleTreasureHunt(Guid.NewGuid(), "Already Inactive");
        var store = new InMemoryMissionStore(mission);
        var handler = new DeactivateMissionCommandHandler(store);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(new DeactivateMissionCommand(mission.Id), CancellationToken.None));

        Assert.Equal("mission_already_inactive", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
        Assert.Equal(0, store.PersistCallCount);
    }
}
