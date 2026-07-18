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
    private readonly Mock<ILiveSessionReadRepository> _liveSessionRepositoryMock;
    private readonly ListSessionTeamsHandler _handler;

    public ListSessionTeamsHandlerTests()
    {
        _liveSessionRepositoryMock = new Mock<ILiveSessionReadRepository>();
        _handler = new ListSessionTeamsHandler(_liveSessionRepositoryMock.Object, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WhenJoinCodeIsInvalid_ThrowsNotFoundDomainException()
    {
        var request = new ListSessionTeamsQuery("INVALID");
        var ex = await Assert.ThrowsAsync<UmbralDomainException>(() =>
            _handler.Handle(request, CancellationToken.None));

        Assert.Equal("join_code_length_invalid", ex.Code);
    }
}




