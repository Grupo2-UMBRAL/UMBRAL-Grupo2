using MissionManagement.Application.Features.Missions.Commands.CreateMission;
using MissionManagement.Domain.Missions;
using Umbral.ServiceDefaults;
using Xunit;

namespace MissionManagement.UnitTests;

public sealed class CreateMissionCommandHandlerTests
{
    [Fact]
    public async Task Handle_RejectsNullRequest()
    {
        var handler = new CreateMissionCommandHandler(new InMemoryMissionStore());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PersistsNewMission_AndReturnsInactiveResponse()
    {
        var store = new InMemoryMissionStore();
        var handler = new CreateMissionCommandHandler(store);
        var command = new CreateMissionCommand("City Circuit", "Route through control points.", 75);

        var response = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("City Circuit", response.Name);
        Assert.False(response.IsActive);
        Assert.Empty(response.Items);
        Assert.Equal(1, store.PersistCallCount);
        Assert.Single(store.Missions);
    }

    [Fact]
    public async Task Handle_ThrowsConflict_WhenNameAlreadyTaken()
    {
        var store = new InMemoryMissionStore(Mission.Create(Guid.NewGuid(), "Taken", "Existing.", 30));
        var handler = new CreateMissionCommandHandler(store);
        var command = new CreateMissionCommand("Taken", "Another.", 45);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(command, CancellationToken.None));

        Assert.Equal("mission_name_duplicate", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
        Assert.Equal(0, store.PersistCallCount);
    }
}
