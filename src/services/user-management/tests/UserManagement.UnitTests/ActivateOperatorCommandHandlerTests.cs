using Moq;
using Umbral.ServiceDefaults;
using UserManagement.Application.Abstractions;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Features.Operators.Commands.ActivateOperator;
using Xunit;

namespace UserManagement.UnitTests;

public sealed class ActivateOperatorCommandHandlerTests
{
    private readonly Mock<IOperatorAdministrationPort> _portMock = new();
    private readonly ActivateOperatorCommandHandler _handler;

    public ActivateOperatorCommandHandlerTests()
    {
        _handler = new ActivateOperatorCommandHandler(_portMock.Object);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        _portMock.Setup(port => port.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(
            () => _handler.Handle(new ActivateOperatorCommand("missing"), CancellationToken.None));

        Assert.Equal("operator_user_not_found", exception.Code);
        Assert.Equal(UmbralFailureCategory.NotFound, exception.Category);
    }

    [Fact]
    public async Task Handle_InactiveOperator_EnablesViaPort_AndTrimsUserId()
    {
        var inactive = new OperatorDto("operator-1", "ana", "ana@example.com", "Ana", "Silva", false);
        var active = inactive with { IsActive = true };
        _portMock.Setup(port => port.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([inactive]);
        _portMock.Setup(port => port.SetUserEnabledAsync("operator-1", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(active);

        var result = await _handler.Handle(new ActivateOperatorCommand("  operator-1  "), CancellationToken.None);

        Assert.Equal(active, result);
        _portMock.Verify(port => port.SetUserEnabledAsync("operator-1", true, It.IsAny<CancellationToken>()), Times.Once);
    }
}
