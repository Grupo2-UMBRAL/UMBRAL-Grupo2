using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Moq;
using SessionManagement.Application.Abstractions;
using SessionManagement.Application.Abstractions.Realtime;
using SessionManagement.Application.Features.SessionEnrollment;
using SessionManagement.Application.Features.SessionLifecycle;
using SessionManagement.Domain.LiveSessions;
using Umbral.ServiceDefaults;
using Xunit;

namespace SessionManagement.UnitTests.Features.SessionEnrollment;

public class RegisterTeamByOperatorHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRepository<LiveSession>> _liveSessionRepositoryMock;
    private readonly Mock<ISessionRealtimeNotifier> _realtimeNotifierMock;
    private readonly RegisterTeamByOperatorHandler _handler;

    public RegisterTeamByOperatorHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _liveSessionRepositoryMock = new Mock<IRepository<LiveSession>>();
        _realtimeNotifierMock = new Mock<ISessionRealtimeNotifier>();

        var timeProvider = TimeProvider.System;
        _handler = new RegisterTeamByOperatorHandler(
            _unitOfWorkMock.Object,
            _liveSessionRepositoryMock.Object,
            timeProvider,
            _realtimeNotifierMock.Object);
    }

    [Fact]
    public async Task Handle_WhenLiveSessionDoesNotExist_ThrowsNotFoundDomainException()
    {
        // Arrange
        var request = new RegisterTeamByOperatorCommand(Guid.NewGuid(), "Team A");
        var emptyList = new List<LiveSession>().AsTestAsyncQueryable();
        _liveSessionRepositoryMock.Setup(m => m.SingleOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LiveSession, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync((LiveSession)null);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.Provider).Returns(emptyList.Provider);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.Expression).Returns(emptyList.Expression);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.ElementType).Returns(emptyList.ElementType);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.GetEnumerator()).Returns(emptyList.GetEnumerator());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            _handler.Handle(request, CancellationToken.None));

        Assert.Equal(UmbralFailureCategory.NotFound, ex.Category);
        Assert.Equal("live_session_not_found", ex.Code);
    }

    [Fact]
    public async Task Handle_WhenSessionIsFoundAndValid_RegistersTeamAndNotifies()
    {
        // Arrange
        var liveSession = LiveSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Mission 1", "Desc", TimeProvider.System.GetUtcNow(), TimeProvider.System.GetUtcNow(), new[] { SessionManagement.Domain.LiveSessions.LiveSessionStage.Create(Guid.NewGuid(), "Stage1", 1, 1, 60, "Easy", "Type1", "Prompt") });
        liveSession.AssignJoinCode(SessionManagement.Domain.LiveSessions.JoinCode.Parse("ABCDE2"));
        liveSession.OpenEnrollmentWindow(TimeProvider.System.GetUtcNow());
        
        var request = new RegisterTeamByOperatorCommand(liveSession.Id, "Team Alpha");
        var sessionsList = new List<LiveSession> { liveSession }.AsTestAsyncQueryable();
        
        _liveSessionRepositoryMock.Setup(m => m.SingleOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LiveSession, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(liveSession);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.Provider).Returns(sessionsList.Provider);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.Expression).Returns(sessionsList.Expression);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.ElementType).Returns(sessionsList.ElementType);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.GetEnumerator()).Returns(sessionsList.GetEnumerator());

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Team Alpha", result.TeamName);
        Assert.Equal(liveSession.Id, result.LiveSessionId);
        
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtimeNotifierMock.Verify(n => n.NotifySessionStateChangedAsync(It.IsAny<LiveSessionStateChangedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(liveSession.SessionTeams);
    }
}









