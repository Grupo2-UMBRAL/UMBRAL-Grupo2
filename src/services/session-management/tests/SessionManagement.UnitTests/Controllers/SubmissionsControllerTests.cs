using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using SessionManagement.Api.Controllers;
using SessionManagement.Application.Features.EvidenceSubmissions;
using System.Runtime.Serialization;

namespace SessionManagement.UnitTests.Controllers;

public class SubmissionsControllerTests
{
    private readonly Mock<ISender> _senderMock;
    private readonly SubmissionsController _controller;

    public SubmissionsControllerTests()
    {
        _senderMock = new Mock<ISender>();
        _controller = new SubmissionsController(_senderMock.Object);
    }

    private T GetUninitialized<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    [Fact]
    public async Task Override_ReturnsOkResult()
    {
        var submissionId = Guid.NewGuid();
        var request = new OverrideValidationOutcomeRequest(true, "Reason");
        var expectedResponse = GetUninitialized<OverrideValidationOutcomeResponse>();

        _senderMock.Setup(s => s.Send(It.IsAny<OverrideValidationOutcomeCommand>(), default))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.Override(submissionId, request, default);
        Assert.IsType<OkObjectResult>(result.Result);
    }
}
