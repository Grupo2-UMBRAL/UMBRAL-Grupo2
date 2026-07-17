using Microsoft.AspNetCore.Mvc;
using SessionManagement.Api.Controllers;
using Xunit;

namespace SessionManagement.UnitTests.Controllers;

public class SmokeControllerTests
{
    private readonly SmokeController _controller;

    public SmokeControllerTests()
    {
        _controller = new SmokeController();
    }

    [Fact]
    public void SmokeAdministrator_ReturnsOk()
    {
        var result = _controller.SmokeAdministrator();
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void SmokeOperator_ReturnsOk()
    {
        var result = _controller.SmokeOperator();
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void SmokeParticipant_ReturnsOk()
    {
        var result = _controller.SmokeParticipant();
        Assert.IsType<OkResult>(result);
    }
}
