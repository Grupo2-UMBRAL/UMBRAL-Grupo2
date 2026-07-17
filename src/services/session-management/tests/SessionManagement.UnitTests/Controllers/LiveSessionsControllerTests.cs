using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using SessionManagement.Api.Controllers;
using SessionManagement.Application.Features.Hints;
using SessionManagement.Application.Features.LiveSessions;
using SessionManagement.Application.Features.Penalties;
using SessionManagement.Application.Features.SessionEnrollment;
using SessionManagement.Application.Features.SessionLifecycle;
using SessionManagement.Application.Features.SessionSnapshots;
using System.Runtime.Serialization;

namespace SessionManagement.UnitTests.Controllers;

public class LiveSessionsControllerTests
{
    private readonly Mock<ISender> _senderMock;
    private readonly LiveSessionsController _controller;

    public LiveSessionsControllerTests()
    {
        _senderMock = new Mock<ISender>();
        _controller = new LiveSessionsController(_senderMock.Object);
    }

    private T GetUninitialized<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    [Fact]
    public async Task List_ReturnsOkResult()
    {
        _senderMock.Setup(s => s.Send(It.IsAny<ListLiveSessionsQuery>(), default))
            .ReturnsAsync(Array.Empty<LiveSessionResponse>());

        var result = await _controller.List(default);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetById_ReturnsOkResult()
    {
        var id = Guid.NewGuid();
        _senderMock.Setup(s => s.Send(It.IsAny<GetLiveSessionByIdQuery>(), default))
            .ReturnsAsync(GetUninitialized<LiveSessionResponse>());

        var result = await _controller.GetById(id, default);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_ReturnsCreatedResult()
    {
        var request = new CreateLiveSessionRequest(Guid.NewGuid(), "Name", DateTime.UtcNow, new List<Guid>());
        var expectedResponse = GetUninitialized<LiveSessionResponse>();

        _senderMock.Setup(s => s.Send(It.IsAny<CreateLiveSessionCommand>(), default))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.Create(request, default);
        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal($"api/session-management/live-sessions/{expectedResponse.Id}", createdResult.Location);
    }

    [Theory]
    [InlineData("Start")]
    [InlineData("Pause")]
    [InlineData("Resume")]
    [InlineData("Finalize")]
    [InlineData("Cancel")]
    public async Task LifecycleActions_ReturnOkResult(string action)
    {
        var id = Guid.NewGuid();
        _senderMock.Setup(s => s.Send(It.IsAny<TransitionLiveSessionStateCommand>(), default))
            .ReturnsAsync(GetUninitialized<LiveSessionStateResponse>());

        ActionResult<LiveSessionStateResponse> result = action switch
        {
            "Start" => await _controller.Start(id, default),
            "Pause" => await _controller.Pause(id, default),
            "Resume" => await _controller.Resume(id, default),
            "Finalize" => await _controller.Finalize(id, default),
            "Cancel" => await _controller.Cancel(id, default),
            _ => throw new NotImplementedException()
        };

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task ApplyPenalty_ReturnsOkResult()
    {
        var id = Guid.NewGuid();
        var request = new ApplyPenaltyRequest(Guid.NewGuid(), Guid.NewGuid(), "Minor", "Reason");
        _senderMock.Setup(s => s.Send(It.IsAny<ApplyPenaltyCommand>(), default))
            .ReturnsAsync(GetUninitialized<ApplyPenaltyResponse>());

        var result = await _controller.ApplyPenalty(id, request, default);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task RegisterTeam_ReturnsCreatedResult()
    {
        var id = Guid.NewGuid();
        var request = new RegisterTeamByOperatorRequest("Team Name");
        var expectedResponse = GetUninitialized<RegisterTeamByOperatorResponse>();

        _senderMock.Setup(s => s.Send(It.IsAny<RegisterTeamByOperatorCommand>(), default))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.RegisterTeam(id, request, default);
        var createdResult = Assert.IsType<CreatedResult>(result.Result);
    }

    [Fact]
    public async Task GetOverview_ReturnsOkResult()
    {
        var id = Guid.NewGuid();
        _senderMock.Setup(s => s.Send(It.IsAny<GetLiveSessionOverviewQuery>(), default))
            .ReturnsAsync(GetUninitialized<LiveSessionOverview>());

        var result = await _controller.GetOverview(id, default);
        Assert.IsType<OkObjectResult>(result.Result);
    }
}
