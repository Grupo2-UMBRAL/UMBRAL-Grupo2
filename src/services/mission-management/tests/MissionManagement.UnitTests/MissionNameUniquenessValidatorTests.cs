using System.Linq.Expressions;
using MissionManagement.Application.Abstractions;
using MissionManagement.Application.Features.Missions;
using MissionManagement.Domain.Missions;
using Moq;
using Umbral.ServiceDefaults;
using Xunit;

namespace MissionManagement.UnitTests;

public sealed class MissionNameUniquenessValidatorTests
{
    private readonly Mock<IRepository<Mission>> _missions = new();

    [Fact]
    public async Task EnsureAvailableAsync_NoClash_DoesNotThrow()
    {
        _missions
            .Setup(repo => repo.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Mission, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Mission?)null);

        await MissionNameUniquenessValidator.EnsureAvailableAsync(
            _missions.Object, "Unique", excludeMissionId: null, CancellationToken.None);
    }

    [Fact]
    public async Task EnsureAvailableAsync_Clash_ThrowsConflict()
    {
        var existing = Mission.Create(Guid.NewGuid(), "Taken", "Description", 30);
        _missions
            .Setup(repo => repo.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Mission, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            MissionNameUniquenessValidator.EnsureAvailableAsync(
                _missions.Object, "Taken", excludeMissionId: Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("mission_name_duplicate", exception.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, exception.Category);
    }
}
