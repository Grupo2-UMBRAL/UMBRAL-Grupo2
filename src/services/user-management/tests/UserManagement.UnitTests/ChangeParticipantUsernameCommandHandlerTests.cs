using Moq;
using Umbral.ServiceDefaults;
using UserManagement.Application.Abstractions;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Features.Participants.Commands.ChangeParticipantUsername;
using Xunit;

namespace UserManagement.UnitTests;

public sealed class ChangeParticipantUsernameCommandHandlerTests
{
    private readonly Mock<ICurrentParticipantIdentity> _identityMock = new();
    private readonly Mock<IParticipantAdministrationPort> _portMock = new();
    private readonly ChangeParticipantUsernameCommandHandler _handler;

    public ChangeParticipantUsernameCommandHandlerTests()
    {
        _identityMock.Setup(identity => identity.GetRequiredParticipantUserId()).Returns("user-1");
        _handler = new ChangeParticipantUsernameCommandHandler(_identityMock.Object, _portMock.Object);
    }

    private Task<ParticipantProfileDto> Handle(string username) =>
        _handler.Handle(new ChangeParticipantUsernameCommand(username), CancellationToken.None);

    // The pre-check exists so the participant vocabulary is born in Application, where it belongs,
    // rather than being translated back from a provider 409.
    [Fact]
    public async Task Handle_UsernameHeldByAnotherUser_ThrowsConflict_WithoutWriting()
    {
        _portMock
            .Setup(port => port.FindUserIdByUsernameAsync("taken", It.IsAny<CancellationToken>()))
            .ReturnsAsync("someone-else");

        var ex = await Assert.ThrowsAsync<UmbralDomainException>(() => Handle("taken"));

        Assert.Equal("participant_username_taken", ex.Code);
        Assert.Equal(UmbralFailureCategory.Conflict, ex.Category);
        _portMock.Verify(
            port => port.UpdateUsernameAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // Where this parts ways with the creation flow it mirrors: on create, any match is a conflict. On
    // rename, matching yourself is just a no-op re-submit, and 409-ing your own handle back at you
    // would be a lie.
    [Fact]
    public async Task Handle_UsernameAlreadyOwnedBySelf_Proceeds()
    {
        _portMock
            .Setup(port => port.FindUserIdByUsernameAsync("pao.rojas", It.IsAny<CancellationToken>()))
            .ReturnsAsync("user-1");
        _portMock
            .Setup(port => port.UpdateUsernameAsync("user-1", "pao.rojas", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParticipantProfileDto("user-1", "pao.rojas", "pao@example.cl", true));

        var result = await Handle("pao.rojas");

        Assert.Equal("pao.rojas", result.Username);
    }

    [Fact]
    public async Task Handle_FreeUsername_UpdatesTrimmed_ForTheAuthenticatedParticipant()
    {
        var profile = new ParticipantProfileDto("user-1", "nuevo.nombre", "pao@example.cl", true);
        _portMock
            .Setup(port => port.FindUserIdByUsernameAsync("nuevo.nombre", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _portMock
            .Setup(port => port.UpdateUsernameAsync("user-1", "nuevo.nombre", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await Handle("  nuevo.nombre  ");

        Assert.Equal(profile, result);
        _portMock.Verify(
            port => port.UpdateUsernameAsync("user-1", "nuevo.nombre", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
