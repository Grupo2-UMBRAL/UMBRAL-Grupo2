using UserManagement.Application.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Umbral.ServiceDefaults;
using UserManagement.Application.Abstractions;
using UserManagement.Application.Features.Participants.Commands.CreateParticipant;
using UserManagement.Domain.Entities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace UserManagement.UnitTests;

public class CreateParticipantCommandHandlerTests
{
    private readonly Mock<IOperatorAdministrationPort> _portMock;
    private readonly Mock<IEmailNotificationService> _emailMock;
    private readonly Mock<ILogger<CreateParticipantCommandHandler>> _loggerMock;
    private readonly CreateParticipantCommandHandler _handler;

    public CreateParticipantCommandHandlerTests()
    {
        _portMock = new Mock<IOperatorAdministrationPort>();
        _emailMock = new Mock<IEmailNotificationService>();
        _loggerMock = new Mock<ILogger<CreateParticipantCommandHandler>>();
        _handler = new CreateParticipantCommandHandler(_portMock.Object, _emailMock.Object, _loggerMock.Object);
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

        _portMock.Setup(p => p.GetUserByIdAsync("player-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(participantUser);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(participantUser);
        _emailMock.Verify(e => e.SendParticipantWelcomeAsync(
            "test@example.com",
            "testuser",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmailFails_StillReturnsParticipant()
    {
        // Arrange
        var command = new CreateParticipantCommand(
            Username: "testuser",
            Email: "test@example.com",
            Password: "Password123!");

        var createdUser = new CreatedUserReference("player-123");
        var participantUser = new OperatorUser("player-123", "testuser", "test@example.com", "testuser", "Jugador", true);

        _portMock.Setup(p => p.FindUsersByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorUser>());
        _portMock.Setup(p => p.FindUsersByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorUser>());
        _portMock.Setup(p => p.CreateUserAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdUser);
        _portMock.Setup(p => p.AssignParticipantRoleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _portMock.Setup(p => p.GetUserByIdAsync("player-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(participantUser);

        _emailMock.Setup(e => e.SendParticipantWelcomeAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SMTP down"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(participantUser.Id);
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
