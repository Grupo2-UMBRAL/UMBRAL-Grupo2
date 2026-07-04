using UserManagement.Application.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Umbral.ServiceDefaults;
using UserManagement.Application.Features.Participants.Commands.CreateParticipant;
using UserManagement.Domain.Entities;
using Xunit;

namespace UserManagement.UnitTests;

public class CreateParticipantCommandHandlerTests
{
    private readonly Mock<IOperatorAdministrationPort> _portMock;
    private readonly CreateParticipantCommandHandler _handler;

    public CreateParticipantCommandHandlerTests()
    {
        _portMock = new Mock<IOperatorAdministrationPort>();
        _handler = new CreateParticipantCommandHandler(_portMock.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesUserAndAssignsParticipantRole()
    {
        // Arrange
        var command = new CreateParticipantCommand("player1", "player1@example.com", "SecurePass123!");

        _portMock.Setup(p => p.FindUsersByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorUser>());

        _portMock.Setup(p => p.FindUsersByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorUser>());

        var createdReference = new CreatedUserReference("player-123");
        _portMock.Setup(p => p.CreateUserAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdReference);

        var finalUser = new OperatorUser("player-123", "player1", "player1@example.com", "player1", "Jugador", true);
        _portMock.Setup(p => p.GetUserByIdAsync("player-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(finalUser);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("player-123", result.Id);

        _portMock.Verify(p => p.AssignParticipantRoleAsync("player-123", It.IsAny<CancellationToken>()), Times.Once);
        _portMock.Verify(p => p.AssignOperatorRoleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsDomainException()
    {
        // Arrange
        var command = new CreateParticipantCommand("player1", "duplicate@example.com", "SecurePass123!");

        _portMock.Setup(p => p.FindUsersByEmailAsync("duplicate@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorUser> { new OperatorUser("other-1", "other", "duplicate@example.com", "O", "T", true) });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UmbralDomainException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Equal("participant_email_duplicate", ex.Code);
    }

    [Fact]
    public async Task Handle_ShortPassword_ThrowsValidationException()
    {
        // Arrange
        var command = new CreateParticipantCommand("player1", "player1@example.com", "short");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UmbralDomainException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Equal("participant_password_too_short", ex.Code);
    }
}
