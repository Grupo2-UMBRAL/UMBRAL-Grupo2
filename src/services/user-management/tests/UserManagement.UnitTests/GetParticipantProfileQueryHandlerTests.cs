using Moq;
using UserManagement.Application.Abstractions;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Features.Participants.Queries.GetParticipantProfile;
using Xunit;

namespace UserManagement.UnitTests;

public sealed class GetParticipantProfileQueryHandlerTests
{
    private readonly Mock<ICurrentParticipantIdentity> _identityMock = new();
    private readonly Mock<IParticipantAdministrationPort> _portMock = new();
    private readonly GetParticipantProfileQueryHandler _handler;

    public GetParticipantProfileQueryHandlerTests()
    {
        _handler = new GetParticipantProfileQueryHandler(_identityMock.Object, _portMock.Object);
    }

    // The query carries no user id by design: it comes from the `sub` claim, which is what makes an
    // IDOR impossible here rather than merely unlikely. Nothing the caller sends picks the account.
    [Fact]
    public async Task Handle_ReadsTheProfileOfTheAuthenticatedParticipant()
    {
        var profile = new ParticipantProfileDto("user-1", "pao.rojas", "pao.rojas@example.cl", true);
        _identityMock.Setup(identity => identity.GetRequiredParticipantUserId()).Returns("user-1");
        _portMock
            .Setup(port => port.GetParticipantProfileAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        var result = await _handler.Handle(new GetParticipantProfileQuery(), CancellationToken.None);

        Assert.Equal(profile, result);
        _portMock.Verify(
            port => port.GetParticipantProfileAsync("user-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
