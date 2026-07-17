using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Umbral.ServiceDefaults.UnitTests;

public class UmbralExceptionHandlerTests
{
    private readonly Mock<ILogger<UmbralExceptionHandler>> _loggerMock;
    private readonly UmbralExceptionHandler _handler;

    public UmbralExceptionHandlerTests()
    {
        _loggerMock = new Mock<ILogger<UmbralExceptionHandler>>();
        _handler = new UmbralExceptionHandler(_loggerMock.Object);
    }

    [Fact]
    public async Task TryHandleAsync_WithNonUmbralException_ReturnsFalse()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var exception = new Exception("General failure");

        // Act
        var result = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(UmbralFailureCategory.Validation, StatusCodes.Status400BadRequest, "Validation failure")]
    [InlineData(UmbralFailureCategory.Unauthorized, StatusCodes.Status401Unauthorized, "Authentication failure")]
    [InlineData(UmbralFailureCategory.Forbidden, StatusCodes.Status403Forbidden, "Authorization failure")]
    [InlineData(UmbralFailureCategory.NotFound, StatusCodes.Status404NotFound, "Resource not found")]
    [InlineData(UmbralFailureCategory.Conflict, StatusCodes.Status409Conflict, "Conflict detected")]
    [InlineData(UmbralFailureCategory.Domain, StatusCodes.Status422UnprocessableEntity, "Domain rule violated")]
    [InlineData(UmbralFailureCategory.Technical, StatusCodes.Status500InternalServerError, "Technical failure")]
    public async Task TryHandleAsync_WithUmbralException_MapsToCorrectStatusCodeAndTitle(
        UmbralFailureCategory category, int expectedStatusCode, string expectedTitle)
    {
        // Arrange
        var context = new DefaultHttpContext();
        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;
        
        var exception = new UmbralDomainException("test_code", "test_message", category);

        // Act
        var result = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedStatusCode, context.Response.StatusCode);

        responseStream.Position = 0;
        using var reader = new StreamReader(responseStream);
        var responseBody = await reader.ReadToEndAsync();
        
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        Assert.Equal(expectedStatusCode, root.GetProperty("status").GetInt32());
        Assert.Equal(expectedTitle, root.GetProperty("title").GetString());
        Assert.Equal("test_message", root.GetProperty("detail").GetString());
        Assert.Equal("test_code", root.GetProperty("code").GetString());
        Assert.Equal(category.ToString(), root.GetProperty("category").GetString());
    }
}
