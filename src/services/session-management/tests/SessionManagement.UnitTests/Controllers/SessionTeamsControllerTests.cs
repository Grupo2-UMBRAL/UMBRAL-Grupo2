using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SessionManagement.Api.Controllers;
using SessionManagement.Application.Features.EvidenceSubmissions;
using SessionManagement.Application.Features.SessionSnapshots;
using Xunit;

namespace SessionManagement.UnitTests.Controllers;

public class SessionTeamsControllerTests
{
    private readonly Mock<ISender> _senderMock;
    private readonly SessionTeamsController _controller;

    public SessionTeamsControllerTests()
    {
        _senderMock = new Mock<ISender>(MockBehavior.Strict);
        _controller = new SessionTeamsController(_senderMock.Object);
    }

    [Fact]
    public async Task GetSnapshot_SendsCorrectQuery_AndReturnsSnapshot()
    {
        // Arrange
        var sessionTeamId = Guid.NewGuid();
        var expectedSnapshot = new SessionTeamSnapshot(
            LiveSessionId: Guid.NewGuid(),
            SessionTeamId: sessionTeamId,
            TeamName: "Alpha",
            SessionState: "Active",
            ProgressState: "InProgress",
            CurrentStage: null,
            VisibleHints: new List<VisibleHintSnapshot>(),
            Sync: new SnapshotSyncMetadata(1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            AllStages: null,
            MemberCount: 4,
            TotalStages: 8);

        _senderMock
            .Setup(s => s.Send(It.Is<GetSessionTeamSnapshotQuery>(q => q.SessionTeamId == sessionTeamId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSnapshot);

        // Act
        var result = await _controller.GetSnapshot(sessionTeamId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedSnapshot = Assert.IsType<SessionTeamSnapshot>(okResult.Value);
        Assert.Equal(sessionTeamId, returnedSnapshot.SessionTeamId);
        Assert.Equal("Alpha", returnedSnapshot.TeamName);
        Assert.Equal("Active", returnedSnapshot.SessionState);
        _senderMock.Verify(s => s.Send(It.IsAny<GetSessionTeamSnapshotQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitEvidence_SendsCommandWithCorrectQrHash_AndReturnsResponse()
    {
        // Arrange
        var sessionTeamId = Guid.NewGuid();
        var qrHash = "abc123-qr-hash";
        var request = new SubmitEvidenceRequest(qrHash);
        var expectedResponse = new SubmitEvidenceResponse(
            LiveSessionId: Guid.NewGuid(),
            SessionTeamId: sessionTeamId,
            EvidenceSubmissionId: Guid.NewGuid(),
            ValidationOutcome: "Accepted",
            ProgressState: "InProgress",
            CurrentStage: null,
            SequenceNumber: 42,
            SubmittedAtUtc: DateTimeOffset.UtcNow);

        _senderMock
            .Setup(s => s.Send(
                It.Is<SubmitEvidenceCommand>(c => c.SessionTeamId == sessionTeamId && c.QrHash == qrHash),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.SubmitEvidence(sessionTeamId, request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedResponse = Assert.IsType<SubmitEvidenceResponse>(okResult.Value);
        Assert.Equal("Accepted", returnedResponse.ValidationOutcome);
        Assert.Equal(sessionTeamId, returnedResponse.SessionTeamId);
        Assert.Equal(42, returnedResponse.SequenceNumber);
        _senderMock.Verify(s => s.Send(It.IsAny<SubmitEvidenceCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitTriviaAnswer_SendsCommandWithCorrectChoiceId_AndReturnsResponse()
    {
        // Arrange
        var sessionTeamId = Guid.NewGuid();
        var choiceId = Guid.NewGuid();
        var request = new SubmitTriviaAnswerRequest(choiceId);
        var expectedResponse = new SubmitEvidenceResponse(
            LiveSessionId: Guid.NewGuid(),
            SessionTeamId: sessionTeamId,
            EvidenceSubmissionId: Guid.NewGuid(),
            ValidationOutcome: "Rejected",
            ProgressState: "InProgress",
            CurrentStage: null,
            SequenceNumber: 7,
            SubmittedAtUtc: DateTimeOffset.UtcNow);

        _senderMock
            .Setup(s => s.Send(
                It.Is<SubmitTriviaAnswerCommand>(c => c.SessionTeamId == sessionTeamId && c.SelectedChoiceId == choiceId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.SubmitTriviaAnswer(sessionTeamId, request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedResponse = Assert.IsType<SubmitEvidenceResponse>(okResult.Value);
        Assert.Equal("Rejected", returnedResponse.ValidationOutcome);
        Assert.Equal(sessionTeamId, returnedResponse.SessionTeamId);
        Assert.Equal(7, returnedResponse.SequenceNumber);
        _senderMock.Verify(s => s.Send(It.IsAny<SubmitTriviaAnswerCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
