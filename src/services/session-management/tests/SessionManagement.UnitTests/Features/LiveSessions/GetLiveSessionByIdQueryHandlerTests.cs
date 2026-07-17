using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Moq;
using SessionManagement.Application.Abstractions;
using SessionManagement.Application.Features.LiveSessions;
using SessionManagement.Domain.LiveSessions;
using Umbral.ServiceDefaults;
using Xunit;

namespace SessionManagement.UnitTests.Features.LiveSessions;

public class GetLiveSessionByIdQueryHandlerTests
{
    private readonly Mock<IRepository<LiveSession>> _repositoryMock;
    private readonly GetLiveSessionByIdQueryHandler _handler;

    public GetLiveSessionByIdQueryHandlerTests()
    {
        _repositoryMock = new Mock<IRepository<LiveSession>>();
        _handler = new GetLiveSessionByIdQueryHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenSessionDoesNotExist_ThrowsNotFoundDomainException()
    {
        var request = new GetLiveSessionByIdQuery(Guid.NewGuid());
        var emptyList = new List<LiveSession>().AsTestAsyncQueryable();
        
        _repositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.Provider).Returns(emptyList.Provider);
        _repositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.Expression).Returns(emptyList.Expression);
        _repositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.ElementType).Returns(emptyList.ElementType);
        _repositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.GetEnumerator()).Returns(emptyList.GetEnumerator());

        var ex = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            _handler.Handle(request, CancellationToken.None));

        Assert.Equal("live_session_not_found", ex.Code);
    }

    [Fact]
    public async Task Handle_WhenSessionExists_ReturnsDto()
    {
        var liveSession = LiveSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Mission 1", "Desc", TimeProvider.System.GetUtcNow(), TimeProvider.System.GetUtcNow(), new[] { SessionManagement.Domain.LiveSessions.LiveSessionStage.Create(Guid.NewGuid(), "Stage1", 1, 1, 60, "Easy", "Type1", "Prompt") });
        var request = new GetLiveSessionByIdQuery(liveSession.Id);
        var list = new List<LiveSession> { liveSession }.AsTestAsyncQueryable();
        
        _repositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.Provider).Returns(list.Provider);
        _repositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.Expression).Returns(list.Expression);
        _repositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.ElementType).Returns(list.ElementType);
        _repositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.GetEnumerator()).Returns(list.GetEnumerator());

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(liveSession.Id, result.Id);
        Assert.Equal(liveSession.MissionId, result.MissionId);
        Assert.Equal(liveSession.State.Value, result.State);
    }
}





