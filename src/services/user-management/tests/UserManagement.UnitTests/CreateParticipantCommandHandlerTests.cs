using UserManagement.Application.Abstractions;
using UserManagement.Application.Common.Dtos;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Umbral.ServiceDefaults;
using UserManagement.Application.Features.Participants.Commands.CreateParticipant;

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
        var flowHandler = new UserManagement.Application.Common.Handlers.UserCreationFlowHandler(_portMock.Object); _handler = new CreateParticipantCommandHandler(flowHandler, _emailMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesUserAndAssignsParticipantRole()
    {
        // Arrange
        var command = new CreateParticipantCommand("player1", "player1@example.com", "SecurePass123!");

        _portMock.Setup(p => p.FindUsersByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());

        _portMock.Setup(p => p.FindUsersByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());

        var createdReference = "player-123";
        _portMock.Setup(p => p.CreateUserAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdReference);

        var participantUser = new OperatorDto("player-123", "player1", "player1@example.com", "player1", "Jugador", true);
        _portMock.Setup(p => p.GetUserByIdAsync("player-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(participantUser);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("player-123", result.UserId);
        _emailMock.Verify(e => e.SendParticipantWelcomeAsync(
            "player1@example.com",
            "player1",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmailFails_StillReturnsParticipant()
    {
        // Arrange
        var command = new CreateParticipantCommand("testuser", "test@example.com", "Password123!");

        var createdUser = "player-123";
        var participantUser = new OperatorDto("player-123", "testuser", "test@example.com", "testuser", "Jugador", true);

        _portMock.Setup(p => p.FindUsersByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());
        _portMock.Setup(p => p.FindUsersByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());
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
        Assert.NotNull(result);
        Assert.Equal(participantUser.Id, result.UserId);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsDomainException()
    {
        // Arrange
        var command = new CreateParticipantCommand("player1", "duplicate@example.com", "SecurePass123!");

        _portMock.Setup(p => p.FindUsersByEmailAsync("duplicate@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto> { new OperatorDto("other-1", "other", "duplicate@example.com", "O", "T", true) });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UmbralDomainException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Equal("participant_email_duplicate", ex.Code);
    }

    // Input validation (password rules, username/email format) now lives in the FluentValidation
    // pipeline and is covered by CreateParticipantCommandValidatorTests; the handler no longer
    // re-validates, so those cases are asserted at the validator level.
}
