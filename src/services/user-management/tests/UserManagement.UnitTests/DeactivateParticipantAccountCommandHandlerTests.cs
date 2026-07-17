using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbral.ServiceDefaults;
using UserManagement.Application.Abstractions;
using UserManagement.Application.Features.Participants.Commands.DeactivateParticipantAccount;
using Xunit;

namespace UserManagement.UnitTests;

/// <summary>
/// Deactivation drives two remote effects with no transaction around them, so the split between what
/// must succeed and what is best effort is the behaviour worth pinning down.
/// </summary>
public sealed class DeactivateParticipantAccountCommandHandlerTests
{
    private readonly Mock<IParticipantAdministrationPort> _portMock = new();
    private readonly DeactivateParticipantAccountCommandHandler _handler;

    public DeactivateParticipantAccountCommandHandlerTests()
    {
        var identityMock = new Mock<ICurrentParticipantIdentity>();
        identityMock.Setup(identity => identity.GetRequiredParticipantUserId()).Returns("user-1");
        _handler = new DeactivateParticipantAccountCommandHandler(
            identityMock.Object,
            _portMock.Object,
            NullLogger<DeactivateParticipantAccountCommandHandler>.Instance);
    }

    private Task Handle() => _handler.Handle(new DeactivateParticipantAccountCommand(), CancellationToken.None);

    // Failing the request here would tell someone whose account IS disabled that it could not be
    // disabled: they would either retry for nothing or walk away believing they are still active.
    // The account is already unreachable -- the refresh grant fails against a disabled user, and the
    // residue is one already-issued access token, bounded by accessTokenLifespan. That window is the
    // cheaper of the two wrongs, and it belongs in a log, not in the HTTP contract.
    [Fact]
    public async Task Handle_LogoutFails_StillSucceeds_WithTheAccountDisabled()
    {
        _portMock
            .Setup(port => port.LogoutParticipantSessionsAsync("user-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UmbralTechnicalException("keycloak_down", "Keycloak refused the logout."));

        await Handle();

        _portMock.Verify(
            port => port.DeactivateParticipantAsync("user-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // The mirror image: if the disable itself failed, nothing happened at all. Saying otherwise would
    // strand a player who thinks they left. Retrying is safe because the disable is idempotent.
    [Fact]
    public async Task Handle_DisableFails_Propagates_AndNeverLogsOut()
    {
        _portMock
            .Setup(port => port.DeactivateParticipantAsync("user-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UmbralTechnicalException("keycloak_down", "Keycloak refused the update."));

        await Assert.ThrowsAsync<UmbralTechnicalException>(Handle);

        _portMock.Verify(
            port => port.LogoutParticipantSessionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DisablesThenLogsOut_TheAuthenticatedParticipant()
    {
        await Handle();

        _portMock.Verify(
            port => port.DeactivateParticipantAsync("user-1", It.IsAny<CancellationToken>()),
            Times.Once);
        _portMock.Verify(
            port => port.LogoutParticipantSessionsAsync("user-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
