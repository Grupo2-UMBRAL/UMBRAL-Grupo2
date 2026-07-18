using MissionManagement.Application.Features.Missions.Commands.UpdateMission;
using MissionManagement.Domain.Missions;
using Umbral.ServiceDefaults;
using Xunit;

namespace MissionManagement.UnitTests;

public sealed class UpdateMissionCommandHandlerTests
{
    [Fact]
    public async Task Handle_RejectsNullRequest()
    {
        var handler = new UpdateMissionCommandHandler(new InMemoryMissionRepository());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenMissionMissing()
    {
        var store = new InMemoryMissionRepository();
        var handler = new UpdateMissionCommandHandler(store);
        var command = new UpdateMissionCommand(Guid.NewGuid(), "Name", "Description", 30);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(command, CancellationToken.None));

        Assert.Equal("mission_not_found", exception.Code);
        Assert.Equal(UmbralFailureCategory.NotFound, exception.Category);
        Assert.Equal(0, store.PersistCallCount);
    }

    [Fact]
    public async Task Handle_UpdatesScalarFields_WhenItemsNull()
    {
        var existing = Mission.Create(Guid.NewGuid(), "Old Mission", "Old.", 45);
        var store = new InMemoryMissionRepository(existing);
        var handler = new UpdateMissionCommandHandler(store);
        var command = new UpdateMissionCommand(existing.Id, "Updated Mission", "Updated.", 95);

        var response = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Updated Mission", response.Name);
        Assert.Equal("Updated.", response.Description);
        Assert.Equal(95, response.MaximumDurationMinutes);
        Assert.Equal(1, store.PersistCallCount);
    }

    [Fact]
    public async Task Handle_ThrowsConflict_WhenRenamedToAnotherMissionsName()
    {
        var target = Mission.Create(Guid.NewGuid(), "Rename Me", "One.", 30);
        var other = Mission.Create(Guid.NewGuid(), "Taken", "Two.", 30);
        var store = new InMemoryMissionRepository(target, other);
        var handler = new UpdateMissionCommandHandler(store);
        var command = new UpdateMissionCommand(target.Id, "Taken", "One.", 30);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            handler.Handle(command, CancellationToken.None));

        Assert.Equal("mission_name_duplicate", exception.Code);
        Assert.Equal(0, store.PersistCallCount);
    }
}
