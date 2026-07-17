using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using UserManagement.Api.Controllers;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Features.Operators.Commands.CreateOperator;
using UserManagement.Application.Features.Operators.Commands.ActivateOperator;
using UserManagement.Application.Features.Operators.Commands.DeactivateOperator;
using UserManagement.Application.Features.Operators.Commands.ResendOperatorInvitation;
using UserManagement.Application.Features.Operators.Commands.SendOperatorPasswordResetLink;
using UserManagement.Application.Features.Operators.Queries.ListOperators;

namespace UserManagement.UnitTests.Controllers;

public class OperatorsControllerTests
{
    private readonly Mock<ISender> _senderMock;
    private readonly OperatorsController _controller;

    public OperatorsControllerTests()
    {
        _senderMock = new Mock<ISender>();
        _controller = new OperatorsController(_senderMock.Object);
    }

    [Fact]
    public async Task ListOperators_ReturnsOkResult_WithOperators()
    {
        // Arrange
        var expectedOperators = new List<OperatorDto>
        {
            new("1", "op1", "op1@test.com", "Operator", "1", true)
        };
        _senderMock.Setup(s => s.Send(It.IsAny<ListOperatorsQuery>(), default))
            .ReturnsAsync(expectedOperators);

        // Act
        var result = await _controller.ListOperators(default);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedOperators = Assert.IsAssignableFrom<IEnumerable<OperatorDto>>(okResult.Value);
        Assert.Single(returnedOperators);
    }

    [Fact]
    public async Task CreateOperator_ReturnsCreatedResult_WithOperator()
    {
        // Arrange
        var command = new CreateOperatorCommand("newop", "new@test.com");
        var expectedOperator = new OperatorDto("123", "newop", "new@test.com", "New", "Operator", true);

        _senderMock.Setup(s => s.Send(command, default))
            .ReturnsAsync(expectedOperator);

        // Act
        var result = await _controller.CreateOperator(command, default);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal($"/api/operators/{expectedOperator.Id}", createdResult.Location);
        Assert.Equal(expectedOperator, createdResult.Value);
    }

    [Fact]
    public async Task DeactivateOperator_ReturnsOkResult()
    {
        // Arrange
        var userId = "123";
        var expectedOperator = new OperatorDto("123", "op", "op@test.com", "Op", "1", false);
        _senderMock.Setup(s => s.Send(It.Is<DeactivateOperatorCommand>(c => c.UserId == userId), default))
            .ReturnsAsync(expectedOperator);

        // Act
        var result = await _controller.DeactivateOperator(userId, default);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expectedOperator, okResult.Value);
    }

    [Fact]
    public async Task ActivateOperator_ReturnsOkResult()
    {
        var userId = "123";
        var expectedOperator = new OperatorDto("123", "op", "op@test.com", "Op", "1", true);
        _senderMock.Setup(s => s.Send(It.Is<ActivateOperatorCommand>(c => c.UserId == userId), default))
            .ReturnsAsync(expectedOperator);

        var result = await _controller.ActivateOperator(userId, default);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expectedOperator, okResult.Value);
    }

    [Fact]
    public async Task ResendOnboardingInvitation_ReturnsOkResult()
    {
        // Arrange
        var userId = "123";
        var expectedOperator = new OperatorDto("123", "op", "op@test.com", "Op", "1", true);
        _senderMock.Setup(s => s.Send(It.Is<ResendOperatorInvitationCommand>(c => c.UserId == userId), default))
            .ReturnsAsync(expectedOperator);

        // Act
        var result = await _controller.ResendOnboardingInvitation(userId, default);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expectedOperator, okResult.Value);
    }

    [Fact]
    public async Task SendPasswordResetLink_ReturnsOkResult()
    {
        // Arrange
        var userId = "123";
        var expectedOperator = new OperatorDto("123", "op", "op@test.com", "Op", "1", true);
        _senderMock.Setup(s => s.Send(It.Is<SendOperatorPasswordResetLinkCommand>(c => c.UserId == userId), default))
            .ReturnsAsync(expectedOperator);

        // Act
        var result = await _controller.SendPasswordResetLink(userId, default);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expectedOperator, okResult.Value);
    }
}
