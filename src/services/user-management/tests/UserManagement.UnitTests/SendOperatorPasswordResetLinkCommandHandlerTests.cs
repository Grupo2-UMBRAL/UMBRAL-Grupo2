using Moq;
using Umbral.ServiceDefaults;
using UserManagement.Application.Abstractions;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Features.Operators.Commands.SendOperatorPasswordResetLink;
using Xunit;

namespace UserManagement.UnitTests;

public sealed class SendOperatorPasswordResetLinkCommandHandlerTests
{
    private readonly Mock<IOperatorAdministrationPort> _portMock = new();
    private readonly SendOperatorPasswordResetLinkCommandHandler _handler;

    public SendOperatorPasswordResetLinkCommandHandlerTests()
    {
        _handler = new SendOperatorPasswordResetLinkCommandHandler(_portMock.Object);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        _portMock.Setup(port => port.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(
            () => _handler.Handle(new SendOperatorPasswordResetLinkCommand("missing"), CancellationToken.None));

        Assert.Equal("operator_user_not_found", exception.Code);
    }

    [Fact]
    public async Task Handle_KnownOperator_SendsPasswordResetLink_AndReturnsOperator()
    {
        var operatorUser = new OperatorDto("operator-1", "ana", "ana@example.com", "Ana", "Silva", true);
        _portMock.Setup(port => port.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([operatorUser]);

        var result = await _handler.Handle(
            new SendOperatorPasswordResetLinkCommand("  operator-1  "),
            CancellationToken.None);

        Assert.Equal(operatorUser, result);
        _portMock.Verify(
            port => port.SendOperatorPasswordResetAsync("operator-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_KeycloakFailure_Propagates()
    {
        var operatorUser = new OperatorDto("operator-1", "ana", "ana@example.com", "Ana", "Silva", true);
        _portMock.Setup(port => port.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([operatorUser]);
        _portMock.Setup(port => port.SendOperatorPasswordResetAsync("operator-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UmbralTechnicalException("user_management_keycloak_admin_failed", "Keycloak failed."));

        await Assert.ThrowsAsync<UmbralTechnicalException>(
            () => _handler.Handle(new SendOperatorPasswordResetLinkCommand("operator-1"), CancellationToken.None));
    }
}
