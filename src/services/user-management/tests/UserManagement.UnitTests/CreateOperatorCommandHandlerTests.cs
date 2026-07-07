using UserManagement.Application.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Umbral.ServiceDefaults;
using UserManagement.Application.Features.Operators.Commands.CreateOperator;
using UserManagement.Domain.Entities;
using Xunit;

namespace UserManagement.UnitTests;

public class CreateOperatorCommandHandlerTests
{
    private readonly Mock<IOperatorAdministrationPort> _portMock;
    private readonly Mock<IEmailNotificationService> _emailMock;
    private readonly CreateOperatorCommandHandler _handler;

    public CreateOperatorCommandHandlerTests()
    {
        _portMock = new Mock<IOperatorAdministrationPort>();
        _emailMock = new Mock<IEmailNotificationService>();
        var loggerMock = new Mock<ILogger<CreateOperatorCommandHandler>>();
        _handler = new CreateOperatorCommandHandler(_portMock.Object, _emailMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesUserAndAssignsRole()
    {
        // Arrange
        var command = new CreateOperatorCommand(
            "jdoe", 
            "jdoe@example.com", 
            "John", 
            "Doe", 
            "SecurePass123!");

        _portMock.Setup(p => p.FindUsersByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorUser>());
            
        _portMock.Setup(p => p.FindUsersByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorUser>());

        var createdReference = new CreatedUserReference("user-123");
        _portMock.Setup(p => p.CreateUserAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdReference);

        var finalUser = new OperatorUser("user-123", "jdoe", "jdoe@example.com", "John", "Doe", true);
        _portMock.Setup(p => p.GetUserByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(finalUser);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("user-123", result.Id);
        
        _portMock.Verify(p => p.AssignOperatorRoleAsync("user-123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsDomainException()
    {
        // Arrange
        var command = new CreateOperatorCommand("jdoe", "duplicate@example.com", "John", "Doe", "SecurePass123!");

        _portMock.Setup(p => p.FindUsersByEmailAsync("duplicate@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorUser> { new OperatorUser("other-1", "other", "duplicate@example.com", "O", "T", true) });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UmbralDomainException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Equal("operator_email_duplicate", ex.Code);
    }

    [Fact]
    public async Task Handle_ValidRequest_SendsCredentialsEmail()
    {
        // Arrange
        var command = new CreateOperatorCommand("jdoe", "jdoe@example.com", "John", "Doe", "SecurePass123!");

        _portMock.Setup(p => p.FindUsersByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorUser>());
        _portMock.Setup(p => p.FindUsersByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorUser>());
        _portMock.Setup(p => p.CreateUserAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreatedUserReference("user-123"));
        _portMock.Setup(p => p.GetUserByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperatorUser("user-123", "jdoe", "jdoe@example.com", "John", "Doe", true));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _emailMock.Verify(e => e.SendOperatorCredentialsAsync(
            "jdoe@example.com", "jdoe", "SecurePass123!", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmailFails_StillReturnsOperator()
    {
        // Arrange
        var command = new CreateOperatorCommand("jdoe", "jdoe@example.com", "John", "Doe", "SecurePass123!");

        _portMock.Setup(p => p.FindUsersByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorUser>());
        _portMock.Setup(p => p.FindUsersByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorUser>());
        _portMock.Setup(p => p.CreateUserAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreatedUserReference("user-123"));
        _portMock.Setup(p => p.GetUserByIdAsync("user-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperatorUser("user-123", "jdoe", "jdoe@example.com", "John", "Doe", true));
        _emailMock.Setup(e => e.SendOperatorCredentialsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — operator is returned despite email failure
        Assert.NotNull(result);
        Assert.Equal("user-123", result.Id);
    }
}
