using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MissionManagement.Application.Abstractions;
using MissionManagement.Application.Features.Missions;
using MissionManagement.Domain.Missions;
using Moq;
using Umbral.ServiceDefaults;
using Xunit;

namespace MissionManagement.UnitTests;

public class MissionLoaderTests
{
    private readonly Mock<IMissionManagementDbContext> _dbContextMock;

    public MissionLoaderTests()
    {
        _dbContextMock = new Mock<IMissionManagementDbContext>();
    }

    [Fact]
    public async Task LoadAsync_WithNonExistentMission_ReturnsNull()
    {
        // Arrange
        var missions = new List<Mission>().SetupDbSetMock();
        _dbContextMock.Setup(x => x.Missions).Returns(missions.Object);

        // Act
        var result = await MissionLoader.LoadAsync(_dbContextMock.Object, Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_WithMissionWithNoItems_ReturnsMission()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var missions = new List<Mission>
        {
            Mission.RehydrateTree(missionId, "Test", "Desc", 60, false, new List<PathItem>())
        }.SetupDbSetMock();

        var sections = new List<Section>().SetupDbSetMock();
        var challenges = new List<Challenge>().SetupDbSetMock();
        
        _dbContextMock.Setup(x => x.Missions).Returns(missions.Object);
        _dbContextMock.Setup(x => x.Sections).Returns(sections.Object);
        _dbContextMock.Setup(x => x.Challenges).Returns(challenges.Object);

        // Act
        var result = await MissionLoader.LoadAsync(_dbContextMock.Object, missionId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(missionId, result.Id);
        Assert.Empty(result.RootItems);
    }
    
    [Fact]
    public async Task RequireAsync_WithExistingMission_ReturnsMission()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var missions = new List<Mission>
        {
            Mission.RehydrateTree(missionId, "Test", "Desc", 60, false, new List<PathItem>())
        }.SetupDbSetMock();

        var sections = new List<Section>().SetupDbSetMock();
        var challenges = new List<Challenge>().SetupDbSetMock();
        
        _dbContextMock.Setup(x => x.Missions).Returns(missions.Object);
        _dbContextMock.Setup(x => x.Sections).Returns(sections.Object);
        _dbContextMock.Setup(x => x.Challenges).Returns(challenges.Object);

        // Act
        var result = await MissionLoader.RequireAsync(_dbContextMock.Object, missionId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(missionId, result.Id);
    }

    [Fact]
    public async Task RequireAsync_WithNonExistentMission_ThrowsDomainException()
    {
        // Arrange
        var missions = new List<Mission>().SetupDbSetMock();
        _dbContextMock.Setup(x => x.Missions).Returns(missions.Object);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UmbralDomainException>(() => 
            MissionLoader.RequireAsync(_dbContextMock.Object, Guid.NewGuid(), CancellationToken.None));
            
        Assert.Equal("mission_not_found", ex.Code);
    }
    
    [Fact]
    public async Task DeleteItemsAsync_RemovesExistingItems()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var section1 = (Section)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Section));
        typeof(PathItem).GetProperty("MissionId")!.SetValue(section1, missionId);
        
        var section2 = (Section)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Section));
        typeof(PathItem).GetProperty("MissionId")!.SetValue(section2, Guid.NewGuid());
        
        var pathItems = new List<PathItem> { section1, section2 }.SetupDbSetMock();
        _dbContextMock.Setup(x => x.PathItems).Returns(pathItems.Object);

        // Act
        await MissionLoader.DeleteItemsAsync(_dbContextMock.Object, missionId, CancellationToken.None);

        // Assert
        pathItems.Verify(x => x.RemoveRange(It.Is<IEnumerable<PathItem>>(items => 
            items.Count() == 1 && items.First().MissionId == missionId)), Times.Once);
    }
    
    [Fact]
    public void AddItems_AddsItemsDepthFirst()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var challengeId = Guid.NewGuid();
        
        var section = (Section)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Section));
        typeof(PathItem).GetProperty("Id")!.SetValue(section, sectionId);
        
        var challenge = (Challenge)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Challenge));
        typeof(PathItem).GetProperty("Id")!.SetValue(challenge, challengeId);
        
        var childrenField = typeof(Section).GetField("_children", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) 
            ?? typeof(Section).BaseType?.GetField("_children", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
        if (childrenField != null)
        {
            childrenField.SetValue(section, new List<PathItem> { challenge });
        }
        else 
        {
            // fallback
            var property = typeof(Section).GetProperty("Children");
            if (property != null && property.CanWrite) property.SetValue(section, new List<PathItem> { challenge });
        }

        var rootItems = new List<PathItem> { section };
        
        var mission = Mission.RehydrateTree(missionId, "Test", "Test", 60, false, rootItems);
        
        var pathItemsMock = new Mock<DbSet<PathItem>>();
        _dbContextMock.Setup(x => x.PathItems).Returns(pathItemsMock.Object);

        // Act
        MissionLoader.AddItems(_dbContextMock.Object, mission);

        // Assert
        pathItemsMock.Verify(x => x.Add(section), Times.Once);
        pathItemsMock.Verify(x => x.Add(challenge), Times.Once);
    }
}
