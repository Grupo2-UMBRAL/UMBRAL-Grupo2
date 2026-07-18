using SessionManagement.Domain.LiveSessions.States;
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
    private readonly Mock<ILiveSessionReadRepository> _repositoryMock;
    private readonly GetLiveSessionByIdQueryHandler _handler;

    public GetLiveSessionByIdQueryHandlerTests()
    {
        _repositoryMock = new Mock<ILiveSessionReadRepository>();
        _handler = new GetLiveSessionByIdQueryHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenSessionDoesNotExist_ThrowsNotFoundDomainException()
    {
        var request = new GetLiveSessionByIdQuery(Guid.NewGuid());
        
        _repositoryMock.Setup(m => m.GetByIdWithSessionTeamsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((LiveSession?)null);

        var ex = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            _handler.Handle(request, CancellationToken.None));

        Assert.Equal("live_session_not_found", ex.Code);
    }

    [Fact]
    public async Task Handle_WhenSessionExists_ReturnsDto()
    {
        var liveSession = LiveSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Mission 1", "Desc", TimeProvider.System.GetUtcNow(), TimeProvider.System.GetUtcNow(), new[] { SessionManagement.Domain.LiveSessions.LiveSessionStage.Create(Guid.NewGuid(), "Stage1", 1, 1, 60, "Easy", "Type1", "Prompt") });
        var request = new GetLiveSessionByIdQuery(liveSession.Id);
        
        _repositoryMock.Setup(m => m.GetByIdWithSessionTeamsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(liveSession);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(liveSession.Id, result.Id);
        Assert.Equal(liveSession.MissionId, result.MissionId);
        Assert.Equal(liveSession.State.Name, result.State);
    }
}





