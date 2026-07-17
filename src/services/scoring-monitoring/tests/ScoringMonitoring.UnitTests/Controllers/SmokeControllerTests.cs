using Microsoft.AspNetCore.Mvc;
using ScoringMonitoring.Api.Controllers;
using Xunit;

namespace ScoringMonitoring.UnitTests.Controllers;

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
