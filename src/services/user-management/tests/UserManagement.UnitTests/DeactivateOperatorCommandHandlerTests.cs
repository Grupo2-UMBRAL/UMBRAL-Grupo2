using Moq;
using Umbral.ServiceDefaults;
using UserManagement.Application.Abstractions;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Features.Operators.Commands.DeactivateOperator;

using Xunit;

namespace UserManagement.UnitTests;

public sealed class DeactivateOperatorCommandHandlerTests
{
    private readonly Mock<IOperatorAdministrationPort> _portMock = new();
    private readonly DeactivateOperatorCommandHandler _handler;

    public DeactivateOperatorCommandHandlerTests()
    {
        _handler = new DeactivateOperatorCommandHandler(_portMock.Object);
    }

    // Blank-user-id validation now runs in the FluentValidation pipeline (see
    // DeactivateOperatorCommandValidatorTests); the handler focuses on lookup and deactivation.

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        _portMock.Setup(p => p.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());

        var ex = await Assert.ThrowsAsync<UmbralDomainException>(
            () => _handler.Handle(new DeactivateOperatorCommand("missing"), CancellationToken.None));

        Assert.Equal("operator_user_not_found", ex.Code);
        Assert.Equal(UmbralFailureCategory.NotFound, ex.Category);
    }

    [Fact]
    public async Task Handle_AlreadyInactive_ReturnsUser_WithoutCallingKeycloak()
    {
        var inactive = new OperatorDto("user-1", "jdoe", "jdoe@example.com", "John", "Doe", false);
        _portMock.Setup(p => p.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto> { inactive });

        var result = await _handler.Handle(new DeactivateOperatorCommand("user-1"), CancellationToken.None);

        Assert.Equal(inactive.Id, result.Id);
        Assert.False(result.IsActive);
        _portMock.Verify(
            p => p.SetUserEnabledAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ActiveUser_DisablesViaPort_AndTrimsUserId()
    {
        var active = new OperatorDto("user-1", "jdoe", "jdoe@example.com", "John", "Doe", true);
        var disabled = active with { IsActive = false };
        _portMock.Setup(p => p.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto> { active });
        _portMock.Setup(p => p.SetUserEnabledAsync("user-1", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(disabled);

        var result = await _handler.Handle(new DeactivateOperatorCommand("  user-1  "), CancellationToken.None);

        Assert.False(result.IsActive);
        _portMock.Verify(p => p.SetUserEnabledAsync("user-1", false, It.IsAny<CancellationToken>()), Times.Once);
    }
}
