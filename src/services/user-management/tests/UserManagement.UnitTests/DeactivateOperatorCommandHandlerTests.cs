using Moq;
using Umbral.ServiceDefaults;
using UserManagement.Application.Abstractions;
using UserManagement.Application.Features.Operators.Commands.DeactivateOperator;
using UserManagement.Domain.Entities;
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_BlankUserId_ThrowsValidation(string userId)
    {
        var ex = await Assert.ThrowsAsync<UmbralDomainException>(
            () => _handler.Handle(new DeactivateOperatorCommand(userId), CancellationToken.None));

        Assert.Equal("operator_user_id_required", ex.Code);
        Assert.Equal(UmbralFailureCategory.Validation, ex.Category);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        _portMock.Setup(p => p.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorUser>());

        var ex = await Assert.ThrowsAsync<UmbralDomainException>(
            () => _handler.Handle(new DeactivateOperatorCommand("missing"), CancellationToken.None));

        Assert.Equal("operator_user_not_found", ex.Code);
        Assert.Equal(UmbralFailureCategory.NotFound, ex.Category);
    }

    [Fact]
    public async Task Handle_AlreadyInactive_ReturnsUser_WithoutCallingKeycloak()
    {
        var inactive = new OperatorUser("user-1", "jdoe", "jdoe@example.com", "John", "Doe", false);
        _portMock.Setup(p => p.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorUser> { inactive });

        var result = await _handler.Handle(new DeactivateOperatorCommand("user-1"), CancellationToken.None);

        Assert.Equal(inactive, result);
        _portMock.Verify(
            p => p.SetUserEnabledAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ActiveUser_DisablesViaPort_AndTrimsUserId()
    {
        var active = new OperatorUser("user-1", "jdoe", "jdoe@example.com", "John", "Doe", true);
        var disabled = active with { IsActive = false };
        _portMock.Setup(p => p.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorUser> { active });
        _portMock.Setup(p => p.SetUserEnabledAsync("user-1", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(disabled);

        var result = await _handler.Handle(new DeactivateOperatorCommand("  user-1  "), CancellationToken.None);

        Assert.False(result.IsActive);
        _portMock.Verify(p => p.SetUserEnabledAsync("user-1", false, It.IsAny<CancellationToken>()), Times.Once);
    }
}
