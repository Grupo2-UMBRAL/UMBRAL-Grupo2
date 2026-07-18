using Moq;
using SessionManagement.Application.Abstractions;
using SessionManagement.Application.Features.SessionEnrollment;
using SessionManagement.Domain.LiveSessions;
using Umbral.ServiceDefaults;
using Xunit;

namespace SessionManagement.UnitTests.Features.SessionEnrollment;

public class EnrollmentWindowHandlersTests
{
    [Fact]
    public async Task OpenEnrollment_WhenSessionNotFound_ThrowsNotFoundDomainException()
    {
        var repository = Repository(null);
        var handler = new OpenEnrollmentWindowHandler(repository.Object, TimeProvider.System);

        var exception = await Assert.ThrowsAsync<UmbralDomainException>(() => handler.Handle(new OpenEnrollmentWindowCommand(Guid.NewGuid()), default));

        Assert.Equal("live_session_not_found", exception.Code);
    }

    [Fact]
    public async Task OpenEnrollment_WhenValid_OpensWindowAndSaves()
    {
        var session = Session();
        session.AssignJoinCode(JoinCode.Parse("ABCDE2"));
        var repository = Repository(session);
        var handler = new OpenEnrollmentWindowHandler(repository.Object, TimeProvider.System);

        await handler.Handle(new OpenEnrollmentWindowCommand(session.Id), default);

        repository.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CloseEnrollment_WhenValid_ClosesWindowAndSaves()
    {
        var session = Session();
        session.AssignJoinCode(JoinCode.Parse("ABCDE2"));
        session.OpenEnrollmentWindow(TimeProvider.System.GetUtcNow());
        var repository = Repository(session);
        var handler = new CloseEnrollmentWindowHandler(repository.Object, TimeProvider.System);

        await handler.Handle(new CloseEnrollmentWindowCommand(session.Id), default);

        repository.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<ILiveSessionRepository> Repository(LiveSession? session)
    {
        var repository = new Mock<ILiveSessionRepository>();
        repository.Setup(store => store.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(session);
        return repository;
    }

    private static LiveSession Session() => LiveSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Mission", "Session", TimeProvider.System.GetUtcNow(), TimeProvider.System.GetUtcNow(), [LiveSessionStage.Create(Guid.NewGuid(), "Play", 1, 1, 60, "Easy", "Trivia", "Prompt")]);
}
