using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Umbral.ServiceDefaults;
using UserManagement.Application.Abstractions;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Common.Handlers;
using Xunit;

namespace UserManagement.UnitTests;

public class UserCreationFlowHandlerTests
{
    private readonly Mock<IOperatorAdministrationPort> _portMock;
    private readonly Mock<ILogger<UserCreationFlowHandler>> _loggerMock;
    private readonly UserCreationFlowHandler _handler;

    public UserCreationFlowHandlerTests()
    {
        _portMock = new Mock<IOperatorAdministrationPort>();
        _loggerMock = new Mock<ILogger<UserCreationFlowHandler>>();
        _handler = new UserCreationFlowHandler(_portMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateUserAsync_WithDuplicateEmail_ThrowsConflictDomainException()
    {
        // Arrange
        _portMock.Setup(p => p.FindUsersByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto> { new OperatorDto(Guid.NewGuid().ToString(), "dup", "dup@email", "a", "b", true) });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            _handler.CreateUserAsync("dup@email", "user", "A", "B", "pass", true, CancellationToken.None));

        Assert.Equal(UmbralFailureCategory.Conflict, ex.Category);
        Assert.Equal("operator_email_duplicate", ex.Code);
    }

    [Fact]
    public async Task CreateUserAsync_WithDuplicateUsername_ThrowsConflictDomainException()
    {
        // Arrange
        _portMock.Setup(p => p.FindUsersByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());

        _portMock.Setup(p => p.FindUsersByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto> { new OperatorDto(Guid.NewGuid().ToString(), "dup", "dup@email", "a", "b", true) });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            _handler.CreateUserAsync("new@email", "dup", "A", "B", "pass", false, CancellationToken.None));

        Assert.Equal(UmbralFailureCategory.Conflict, ex.Category);
        Assert.Equal("participant_username_duplicate", ex.Code);
    }

    [Fact]
    public async Task CreateUserAsync_WhenRoleAssignmentFails_DeletesUserAndThrows()
    {
        // Arrange
        var newUserId = Guid.NewGuid().ToString();
        _portMock.Setup(p => p.FindUsersByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());
        _portMock.Setup(p => p.FindUsersByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());
        
        _portMock.Setup(p => p.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newUserId);

        _portMock.Setup(p => p.AssignOperatorRoleAsync(newUserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Role assignment failed"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _handler.CreateUserAsync("a@b.com", "user", "A", "B", "pass", true, CancellationToken.None));

        _portMock.Verify(p => p.DeleteUserAsync(newUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_WhenRoleAssignmentAndRollbackFail_LogsErrorAndThrows()
    {
        // Arrange
        var newUserId = Guid.NewGuid().ToString();
        _portMock.Setup(p => p.FindUsersByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());
        _portMock.Setup(p => p.FindUsersByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());
        
        _portMock.Setup(p => p.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newUserId);

        _portMock.Setup(p => p.AssignParticipantRoleAsync(newUserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Role assignment failed"));
            
        _portMock.Setup(p => p.DeleteUserAsync(newUserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Rollback failed"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _handler.CreateUserAsync("a@b.com", "user", "A", "B", "pass", false, CancellationToken.None));

        _portMock.Verify(p => p.DeleteUserAsync(newUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_WhenUserNotFoundAfterCreation_ThrowsTechnicalException()
    {
        // Arrange
        var newUserId = Guid.NewGuid().ToString();
        _portMock.Setup(p => p.FindUsersByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());
        _portMock.Setup(p => p.FindUsersByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());
        
        _portMock.Setup(p => p.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newUserId);

        _portMock.Setup(p => p.GetUserByIdAsync(newUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperatorDto?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UmbralTechnicalException>(() =>
            _handler.CreateUserAsync("a@b.com", "user", "A", "B", "pass", true, CancellationToken.None));

        Assert.Equal("operator_user_recovery_failed", ex.Code);
    }

    [Fact]
    public async Task CreateUserAsync_HappyPath_Operator_CreatesAssignsAndReturns()
    {
        // Arrange
        var newUserId = Guid.NewGuid().ToString();
        var expectedUser = new OperatorDto(newUserId, "user", "a@b.com", "A", "B", true);
        
        _portMock.Setup(p => p.FindUsersByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());
        _portMock.Setup(p => p.FindUsersByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());
        
        _portMock.Setup(p => p.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newUserId);

        _portMock.Setup(p => p.GetUserByIdAsync(newUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUser);

        // Act
        var result = await _handler.CreateUserAsync("a@b.com", "user", "A", "B", "pass", true, CancellationToken.None);

        // Assert
        Assert.Equal(expectedUser, result);
        _portMock.Verify(p => p.AssignOperatorRoleAsync(newUserId, It.IsAny<CancellationToken>()), Times.Once);
        _portMock.Verify(p => p.AssignParticipantRoleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    
    [Fact]
    public async Task CreateUserAsync_HappyPath_Participant_CreatesAssignsAndReturns()
    {
        // Arrange
        var newUserId = Guid.NewGuid().ToString();
        var expectedUser = new OperatorDto(newUserId, "user", "a@b.com", "A", "B", true);
        
        _portMock.Setup(p => p.FindUsersByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());
        _portMock.Setup(p => p.FindUsersByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());
        
        _portMock.Setup(p => p.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newUserId);

        _portMock.Setup(p => p.GetUserByIdAsync(newUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUser);

        // Act
        var result = await _handler.CreateUserAsync("a@b.com", "user", "A", "B", "pass", false, CancellationToken.None);

        // Assert
        Assert.Equal(expectedUser, result);
        _portMock.Verify(p => p.AssignParticipantRoleAsync(newUserId, It.IsAny<CancellationToken>()), Times.Once);
        _portMock.Verify(p => p.AssignOperatorRoleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
