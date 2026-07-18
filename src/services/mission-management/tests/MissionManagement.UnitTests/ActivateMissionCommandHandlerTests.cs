using MissionManagement.Application.Features.Missions.Commands.ActivateMission;
using MissionManagement.Domain.Missions;
using Umbral.ServiceDefaults;
using Xunit;

namespace MissionManagement.UnitTests;

public sealed class ActivateMissionCommandHandlerTests
{
    [Fact]
    public async Task Handle_RejectsNullRequest()
    {
        var handler = new ActivateMissionCommandHandler(new InMemoryMissionRepository());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenMissionMissing()
    {
        var store = new InMemoryMissionRepository();
        var handler = new ActivateMissionCommandHandler(store);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(new ActivateMissionCommand(Guid.NewGuid()), CancellationToken.None));

        Assert.Equal("mission_not_found", exception.Code);
        Assert.Equal(0, store.PersistCallCount);
    }

    [Fact]
    public async Task Handle_ActivatesEligibleMission()
    {
        var mission = SampleMissions.SingleTreasureHunt(Guid.NewGuid(), "Activation Mission");
        var store = new InMemoryMissionRepository(mission);
        var handler = new ActivateMissionCommandHandler(store);

        var response = await handler.Handle(new ActivateMissionCommand(mission.Id), CancellationToken.None);

        Assert.True(response.IsActive);
        Assert.Equal(1, store.PersistCallCount);
    }

    [Fact]
    public async Task Handle_ThrowsValidation_WhenMissionHasNoEligiblePlay()
    {
        var mission = Mission.Create(Guid.NewGuid(), "Draft Mission", "No plays.", 25);
        var store = new InMemoryMissionRepository(mission);
        var handler = new ActivateMissionCommandHandler(store);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(new ActivateMissionCommand(mission.Id), CancellationToken.None));

        Assert.Equal("mission_eligible_play_required", exception.Code);
        Assert.Equal(0, store.PersistCallCount);
    }
}
