using Moq;
using Umbral.ServiceDefaults;
using UserManagement.Application.Abstractions;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Features.Operators.Commands.ResendOperatorInvitation;

using Xunit;

namespace UserManagement.UnitTests;

public sealed class ResendOperatorInvitationCommandHandlerTests
{
    private readonly Mock<IOperatorAdministrationPort> _portMock = new();
    private readonly ResendOperatorInvitationCommandHandler _handler;

    public ResendOperatorInvitationCommandHandlerTests()
    {
        _handler = new ResendOperatorInvitationCommandHandler(_portMock.Object);
    }

    // An id that resolves to no Operator (the list is role-scoped) is rejected rather than emailed a link.
    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound_AndDoesNotInvite()
    {
        _portMock.Setup(p => p.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto>());

        var ex = await Assert.ThrowsAsync<UmbralDomainException>(
            () => _handler.Handle(new ResendOperatorInvitationCommand("missing"), CancellationToken.None));

        Assert.Equal("operator_user_not_found", ex.Code);
        Assert.Equal(UmbralFailureCategory.NotFound, ex.Category);
        _portMock.Verify(
            p => p.SendOperatorOnboardingInvitationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_KnownOperator_SendsInvitation_TrimsUserId_AndReturnsOperator()
    {
        var operatorUser = new OperatorDto("user-1", "jdoe", "jdoe@example.com", "John", "Doe", true);
        _portMock.Setup(p => p.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto> { operatorUser });

        var result = await _handler.Handle(new ResendOperatorInvitationCommand("  user-1  "), CancellationToken.None);

        Assert.Equal(operatorUser, result);
        _portMock.Verify(
            p => p.SendOperatorOnboardingInvitationAsync("user-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Unlike creation, a resend is an explicit request: a Keycloak failure surfaces to the Administrator
    // rather than being swallowed.
    [Fact]
    public async Task Handle_InvitationFails_Propagates()
    {
        var operatorUser = new OperatorDto("user-1", "jdoe", "jdoe@example.com", "John", "Doe", true);
        _portMock.Setup(p => p.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorDto> { operatorUser });
        _portMock.Setup(p => p.SendOperatorOnboardingInvitationAsync("user-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UmbralTechnicalException("user_management_keycloak_admin_failed", "boom"));

        await Assert.ThrowsAsync<UmbralTechnicalException>(
            () => _handler.Handle(new ResendOperatorInvitationCommand("user-1"), CancellationToken.None));
    }
}
