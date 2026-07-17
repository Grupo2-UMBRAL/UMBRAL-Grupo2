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

public class ListSessionTeamsHandlerTests
{
    private readonly Mock<IRepository<LiveSession>> _liveSessionRepositoryMock;
    private readonly ListSessionTeamsHandler _handler;

    public ListSessionTeamsHandlerTests()
    {
        _liveSessionRepositoryMock = new Mock<IRepository<LiveSession>>();
        _handler = new ListSessionTeamsHandler(_liveSessionRepositoryMock.Object, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WhenJoinCodeIsInvalid_ThrowsNotFoundDomainException()
    {
        var request = new ListSessionTeamsQuery("INVALID");
        var emptyList = new List<LiveSession>().AsTestAsyncQueryable();

        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.Provider).Returns(emptyList.Provider);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.Expression).Returns(emptyList.Expression);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.ElementType).Returns(emptyList.ElementType);
        _liveSessionRepositoryMock.As<IQueryable<LiveSession>>().Setup(m => m.GetEnumerator()).Returns(emptyList.GetEnumerator());

        var ex = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            _handler.Handle(request, CancellationToken.None));

        Assert.Equal("join_code_length_invalid", ex.Code);
    }
}




