using System.Linq.Expressions;
using MissionManagement.Application.Abstractions;
using MissionManagement.Application.Features.Missions.Commands.UpdateMission;
using MissionManagement.Domain.Missions;
using Moq;
using Umbral.ServiceDefaults;
using Xunit;

namespace MissionManagement.UnitTests;

public sealed class UpdateMissionCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IRepository<Mission>> _missionRepository = new();
    private readonly Mock<IMissionManagementDbContext> _dbContext = new();
    private readonly UpdateMissionCommandHandler _handler;

    public UpdateMissionCommandHandlerTests()
    {
        _handler = new UpdateMissionCommandHandler(
            _unitOfWork.Object,
            _missionRepository.Object,
            _dbContext.Object);
    }

    [Fact]
    public async Task Handle_RejectsNullRequest()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenMissionMissing()
    {
        var command = new UpdateMissionCommand(Guid.NewGuid(), "Name", "Description", 30);

        _missionRepository
            .Setup(repo => repo.SingleOrDefaultAsync(
                It.IsAny<Expression<Func<Mission, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Mission?)null);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            _handler.Handle(command, CancellationToken.None));

        Assert.Equal("mission_not_found", exception.Code);
        Assert.Equal(UmbralFailureCategory.NotFound, exception.Category);
        _unitOfWork.Verify(uow => uow.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
