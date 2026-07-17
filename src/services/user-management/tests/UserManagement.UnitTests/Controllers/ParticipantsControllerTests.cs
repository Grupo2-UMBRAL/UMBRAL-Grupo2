using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using UserManagement.Api.Controllers;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Features.Participants.Commands.CreateParticipant;

namespace UserManagement.UnitTests.Controllers;

public class ParticipantsControllerTests
{
    private readonly Mock<ISender> _senderMock;
    private readonly ParticipantsController _controller;

    public ParticipantsControllerTests()
    {
        _senderMock = new Mock<ISender>();
        _controller = new ParticipantsController(_senderMock.Object);
    }

    [Fact]
    public async Task CreateParticipant_ReturnsCreatedResult_WithParticipant()
    {
        // Arrange
        var command = new CreateParticipantCommand("player1", "player1@test.com", "pass123");
        var expectedParticipant = new ParticipantDto("100", "player1");

        _senderMock.Setup(s => s.Send(command, default))
            .ReturnsAsync(expectedParticipant);

        // Act
        var result = await _controller.CreateParticipant(command, default);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal($"/api/participants/{expectedParticipant.UserId}", createdResult.Location);
        
        // Assert the returned value
        var participantDto = Assert.IsType<ParticipantDto>(createdResult.Value);
        Assert.Equal(expectedParticipant.UserId, participantDto.UserId);
        Assert.Equal(expectedParticipant.Username, participantDto.Username);
    }
}
