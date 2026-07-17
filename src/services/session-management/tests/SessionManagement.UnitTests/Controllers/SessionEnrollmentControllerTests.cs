using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using SessionManagement.Api.Controllers;
using SessionManagement.Application.Features.SessionEnrollment;
using System.Runtime.Serialization;

namespace SessionManagement.UnitTests.Controllers;

public class SessionEnrollmentControllerTests
{
    private readonly Mock<ISender> _senderMock;
    private readonly SessionEnrollmentController _controller;

    public SessionEnrollmentControllerTests()
    {
        _senderMock = new Mock<ISender>();
        _controller = new SessionEnrollmentController(_senderMock.Object);
    }

    private T GetUninitialized<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    [Fact]
    public async Task GenerateJoinCode_ReturnsOkResult()
    {
        var id = Guid.NewGuid();
        _senderMock.Setup(s => s.Send(It.IsAny<GenerateJoinCodeCommand>(), default))
            .ReturnsAsync(GetUninitialized<GenerateJoinCodeResponse>());

        var result = await _controller.GenerateJoinCode(id, default);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task OpenEnrollmentWindow_ReturnsOkResult()
    {
        var id = Guid.NewGuid();
        _senderMock.Setup(s => s.Send(It.IsAny<OpenEnrollmentWindowCommand>(), default))
            .ReturnsAsync(GetUninitialized<EnrollmentWindowResponse>());

        var result = await _controller.OpenEnrollmentWindow(id, default);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task CloseEnrollmentWindow_ReturnsOkResult()
    {
        var id = Guid.NewGuid();
        _senderMock.Setup(s => s.Send(It.IsAny<CloseEnrollmentWindowCommand>(), default))
            .ReturnsAsync(GetUninitialized<EnrollmentWindowResponse>());

        var result = await _controller.CloseEnrollmentWindow(id, default);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task ValidateJoinCode_ReturnsOkResult()
    {
        _senderMock.Setup(s => s.Send(It.IsAny<ValidateJoinCodeQuery>(), default))
            .ReturnsAsync(GetUninitialized<ParticipantEnrollmentStatusResponse>());

        var result = await _controller.ValidateJoinCode("CODE123", default);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task ListSessionTeams_ReturnsOkResult()
    {
        _senderMock.Setup(s => s.Send(It.IsAny<ListSessionTeamsQuery>(), default))
            .ReturnsAsync(GetUninitialized<SessionTeamsResponse>());

        var result = await _controller.ListSessionTeams("CODE123", default);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task RegisterTeam_ReturnsCreatedResult()
    {
        var request = new RegisterTeamRequest("CODE123", "Team A");
        var expectedResponse = GetUninitialized<RegisterTeamResponse>();

        _senderMock.Setup(s => s.Send(It.IsAny<RegisterTeamCommand>(), default))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.RegisterTeam(request, default);
        var createdResult = Assert.IsType<CreatedResult>(result.Result);
    }

    [Fact]
    public async Task JoinSessionTeam_ReturnsOkResult()
    {
        var request = new JoinSessionTeamRequest("CODE123", Guid.NewGuid());
        _senderMock.Setup(s => s.Send(It.IsAny<JoinSessionTeamCommand>(), default))
            .ReturnsAsync(GetUninitialized<JoinSessionTeamResponse>());

        var result = await _controller.JoinSessionTeam(request, default);
        Assert.IsType<OkObjectResult>(result.Result);
    }
}
