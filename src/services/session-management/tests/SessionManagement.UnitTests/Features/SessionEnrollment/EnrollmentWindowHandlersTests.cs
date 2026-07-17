using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


using Moq;
using SessionManagement.Application.Abstractions;
using SessionManagement.Application.Features.SessionEnrollment;
using SessionManagement.Domain.LiveSessions;
using Umbral.ServiceDefaults;
using Xunit;

namespace SessionManagement.UnitTests.Features.SessionEnrollment;

public class EnrollmentWindowHandlersTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IRepository<LiveSession>> _liveSessionRepositoryMock;
    private readonly OpenEnrollmentWindowHandler _openHandler;
    private readonly CloseEnrollmentWindowHandler _closeHandler;

    public EnrollmentWindowHandlersTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _liveSessionRepositoryMock = new Mock<IRepository<LiveSession>>();

        var timeProvider = TimeProvider.System;
        _openHandler = new OpenEnrollmentWindowHandler(
            _unitOfWorkMock.Object,
            _liveSessionRepositoryMock.Object,
            timeProvider);

        _closeHandler = new CloseEnrollmentWindowHandler(
            _unitOfWorkMock.Object,
            _liveSessionRepositoryMock.Object,
            timeProvider);
    }

    [Fact]
    public async Task OpenEnrollment_WhenSessionNotFound_ThrowsNotFoundDomainException()
    {
        var request = new OpenEnrollmentWindowCommand(Guid.NewGuid());
        var emptyList = new List<LiveSession>().AsTestAsyncQueryable();
        
        _liveSessionRepositoryMock.Setup(m => m.SingleOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LiveSession, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync((LiveSession)null);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.Provider).Returns(emptyList.Provider);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.Expression).Returns(emptyList.Expression);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.ElementType).Returns(emptyList.ElementType);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.GetEnumerator()).Returns(emptyList.GetEnumerator());

        var ex = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            _openHandler.Handle(request, CancellationToken.None));

        Assert.Equal("live_session_not_found", ex.Code);
    }

    [Fact]
    public async Task OpenEnrollment_WhenValid_OpensWindowAndSaves()
    {
        var liveSession = LiveSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Miss", "Desc", TimeProvider.System.GetUtcNow(), TimeProvider.System.GetUtcNow(), new[] { SessionManagement.Domain.LiveSessions.LiveSessionStage.Create(Guid.NewGuid(), "Stage1", 1, 1, 60, "Easy", "Type1", "Prompt") });
        liveSession.AssignJoinCode(SessionManagement.Domain.LiveSessions.JoinCode.Parse("ABCDE2"));
        var request = new OpenEnrollmentWindowCommand(liveSession.Id);
        
        var list = new List<LiveSession> { liveSession }.AsTestAsyncQueryable();
        
        _liveSessionRepositoryMock.Setup(m => m.SingleOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LiveSession, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(liveSession);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.Provider).Returns(list.Provider);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.Expression).Returns(list.Expression);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.ElementType).Returns(list.ElementType);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.GetEnumerator()).Returns(list.GetEnumerator());

        var result = await _openHandler.Handle(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result.OpenedAtUtc);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CloseEnrollment_WhenSessionNotFound_ThrowsNotFoundDomainException()
    {
        var request = new CloseEnrollmentWindowCommand(Guid.NewGuid());
        var emptyList = new List<LiveSession>().AsTestAsyncQueryable();
        
        _liveSessionRepositoryMock.Setup(m => m.SingleOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LiveSession, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync((LiveSession)null);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.Provider).Returns(emptyList.Provider);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.Expression).Returns(emptyList.Expression);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.ElementType).Returns(emptyList.ElementType);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.GetEnumerator()).Returns(emptyList.GetEnumerator());

        var ex = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            _closeHandler.Handle(request, CancellationToken.None));

        Assert.Equal("live_session_not_found", ex.Code);
    }

    [Fact]
    public async Task CloseEnrollment_WhenValid_ClosesWindowAndSaves()
    {
        var liveSession = LiveSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Miss", "Desc", TimeProvider.System.GetUtcNow(), TimeProvider.System.GetUtcNow(), new[] { SessionManagement.Domain.LiveSessions.LiveSessionStage.Create(Guid.NewGuid(), "Stage1", 1, 1, 60, "Easy", "Type1", "Prompt") });
        liveSession.AssignJoinCode(SessionManagement.Domain.LiveSessions.JoinCode.Parse("ABCDE2"));
        liveSession.OpenEnrollmentWindow(TimeProvider.System.GetUtcNow()); // Ensure it can be closed

        var request = new CloseEnrollmentWindowCommand(liveSession.Id);
        var list = new List<LiveSession> { liveSession }.AsTestAsyncQueryable();
        
        _liveSessionRepositoryMock.Setup(m => m.SingleOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LiveSession, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(liveSession);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.Provider).Returns(list.Provider);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.Expression).Returns(list.Expression);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.ElementType).Returns(list.ElementType);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.GetEnumerator()).Returns(list.GetEnumerator());

        var result = await _closeHandler.Handle(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result.ClosedAtUtc);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}










