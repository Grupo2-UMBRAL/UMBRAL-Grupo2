using Moq;
using SessionManagement.Application.Abstractions;
using SessionManagement.Application.Abstractions.Realtime;
using SessionManagement.Application.Features.SessionEnrollment;
using SessionManagement.Domain.LiveSessions;
using Umbral.ServiceDefaults;

namespace SessionManagement.UnitTests.Features.SessionEnrollment;

public class RegisterTeamByOperatorHandlerTests
{
    [Fact]
    public async Task Handle_WhenLiveSessionDoesNotExist_ThrowsNotFoundDomainException()
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository.Setup(store => store.GetWithSessionTeamsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((LiveSession?)null);
        var handler = new RegisterTeamByOperatorHandler(repository.Object, TimeProvider.System, Mock.Of<ISessionRealtimeNotifier>());

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() => handler.Handle(new RegisterTeamByOperatorCommand(Guid.NewGuid(), "Team A"), default));

        Assert.Equal("live_session_not_found", exception.Code);
    }

    [Fact]
    public async Task Handle_WhenValid_RegistersTeamAndSaves()
    {
        var session = LiveSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Mission", "Session", TimeProvider.System.GetUtcNow(), TimeProvider.System.GetUtcNow(), [LiveSessionStage.Create(Guid.NewGuid(), "Play", 1, 1, 60, "Easy", "Trivia", "Prompt")]);
        session.AssignJoinCode(JoinCode.Parse("ABCDE2"));
        session.OpenEnrollmentWindow(TimeProvider.System.GetUtcNow());
        var repository = new Mock<ILiveSessionRepository>();
        repository.Setup(store => store.GetWithSessionTeamsAsync(session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var handler = new RegisterTeamByOperatorHandler(repository.Object, TimeProvider.System, Mock.Of<ISessionRealtimeNotifier>());

        await handler.Handle(new RegisterTeamByOperatorCommand(session.Id, "Team A"), default);

        repository.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
