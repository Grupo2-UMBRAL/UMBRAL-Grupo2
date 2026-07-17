using UserManagement.Application.Abstractions;
using UserManagement.Application.Common.Dtos;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Umbral.ServiceDefaults;
using UserManagement.Application.Features.Operators.Commands.CreateOperator;

using Xunit;

namespace UserManagement.UnitTests;

public class CreateOperatorCommandHandlerTests
{
    private readonly Mock<IOperatorAdministrationPort> _portMock;
    private readonly CreateOperatorCommandHandler _handler;

    public CreateOperatorCommandHandlerTests()
    {
        _portMock = new Mock<IOperatorAdministrationPort>();
        var loggerMock = new Mock<ILogger<CreateOperatorCommandHandler>>();
        var flowHandler = new UserManagement.Application.Common.Handlers.UserCreationFlowHandler(
            _portMock.Object,
            Mock.Of<ILogger<UserManagement.Application.Common.Handlers.UserCreationFlowHandler>>());
        _handler = new CreateOperatorCommandHandler(flowHandler, _portMock.Object, loggerMock.Object);
    }

    private void ArrangeNoDuplicatesAndCreation(string createdUserId = "user-123")
    {
        _portMock.Setup(p => p.FindUsersByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());
        _portMock.Setup(p => p.FindUsersByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());
        _portMock.Setup(p => p.CreateUserAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdUserId);
        _portMock.Setup(p => p.GetUserByIdAsync(createdUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperatorDto(createdUserId, "jdoe", "jdoe@example.com", "", "", true));
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesUserAndAssignsRole()
    {
        // Arrange
        var command = new CreateOperatorCommand("jdoe", "jdoe@example.com");
        ArrangeNoDuplicatesAndCreation();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("user-123", result.Id);
        _portMock.Verify(p => p.AssignOperatorRoleAsync("user-123", It.IsAny<CancellationToken>()), Times.Once);
    }

    // The Administrator supplies no secret: the account is created credential-less (null password) and
    // its names are left for the Operator to complete via the onboarding profile step.
    [Fact]
    public async Task Handle_CreatesUserWithoutPasswordOrNames()
    {
        // Arrange
        var command = new CreateOperatorCommand("jdoe", "jdoe@example.com");
        ArrangeNoDuplicatesAndCreation();

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _portMock.Verify(p => p.CreateUserAsync(
            "jdoe", "jdoe@example.com", null, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsDomainException()
    {
        // Arrange
        var command = new CreateOperatorCommand("jdoe", "duplicate@example.com");

        _portMock.Setup(p => p.FindUsersByEmailAsync("duplicate@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto> { new OperatorDto("other-1", "other", "duplicate@example.com", "O", "T", true) });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UmbralDomainException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Equal("operator_email_duplicate", ex.Code);
    }

    // Onboarding replaces the credentials email: Keycloak sends the one-time link, so the handler only
    // fires the invitation and never touches an SMTP notification service.
    [Fact]
    public async Task Handle_ValidRequest_SendsOnboardingInvitation()
    {
        // Arrange
        var command = new CreateOperatorCommand("jdoe", "jdoe@example.com");
        ArrangeNoDuplicatesAndCreation();

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _portMock.Verify(
            p => p.SendOperatorOnboardingInvitationAsync("user-123", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // A failed invitation is ambiguous (Keycloak may already have sent it) and must not delete the
    // account: the operator is returned so the Administrator can resend rather than create a duplicate.
    [Fact]
    public async Task Handle_InvitationFails_StillReturnsOperator()
    {
        // Arrange
        var command = new CreateOperatorCommand("jdoe", "jdoe@example.com");
        ArrangeNoDuplicatesAndCreation();
        _portMock.Setup(p => p.SendOperatorOnboardingInvitationAsync("user-123", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — operator is returned despite the invitation failure
        Assert.NotNull(result);
        Assert.Equal("user-123", result.Id);
        _portMock.Verify(p => p.DeleteUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
