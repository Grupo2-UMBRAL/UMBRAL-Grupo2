using Microsoft.Extensions.Logging;
using Moq;
using Umbral.ServiceDefaults;
using UserManagement.Application.Abstractions;
using UserManagement.Application.Features.Operators.Commands.RotateOperatorPassword;
using UserManagement.Domain.Entities;
using Xunit;

namespace UserManagement.UnitTests;

public sealed class RotateOperatorPasswordCommandHandlerTests
{
    private readonly Mock<IOperatorAdministrationPort> _portMock = new();
    private readonly Mock<IEmailNotificationService> _emailMock = new();
    private readonly RotateOperatorPasswordCommandHandler _handler;

    public RotateOperatorPasswordCommandHandlerTests()
    {
        var loggerMock = new Mock<ILogger<RotateOperatorPasswordCommandHandler>>();
        _handler = new RotateOperatorPasswordCommandHandler(_portMock.Object, _emailMock.Object, loggerMock.Object);
    }

    private void SetupExistingUser(OperatorUser user) =>
        _portMock.Setup(p => p.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorUser> { user });

    [Fact]
    public async Task Handle_BlankUserId_ThrowsValidation_BeforeTouchingPort()
    {
        var ex = await Assert.ThrowsAsync<UmbralDomainException>(
            () => _handler.Handle(new RotateOperatorPasswordCommand("   ", "ValidPass123"), CancellationToken.None));

        Assert.Equal("operator_user_id_required", ex.Code);
        _portMock.Verify(p => p.ListOperatorsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsNotFound()
    {
        _portMock.Setup(p => p.ListOperatorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperatorUser>());

        var ex = await Assert.ThrowsAsync<UmbralDomainException>(
            () => _handler.Handle(new RotateOperatorPasswordCommand("missing", "ValidPass123"), CancellationToken.None));

        Assert.Equal("operator_user_not_found", ex.Code);
    }

    [Theory]
    [InlineData(null, "operator_password_required")]
    [InlineData("   ", "operator_password_required")]
    [InlineData("short", "operator_password_too_short")]
    public async Task Handle_InvalidPassword_ThrowsValidation(string? password, string expectedCode)
    {
        SetupExistingUser(new OperatorUser("user-1", "jdoe", "jdoe@example.com", "John", "Doe", true));

        var ex = await Assert.ThrowsAsync<UmbralDomainException>(
            () => _handler.Handle(new RotateOperatorPasswordCommand("user-1", password), CancellationToken.None));

        Assert.Equal(expectedCode, ex.Code);
        _portMock.Verify(
            p => p.RotateOperatorPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_PasswordOver128Chars_ThrowsTooLong()
    {
        SetupExistingUser(new OperatorUser("user-1", "jdoe", "jdoe@example.com", "John", "Doe", true));

        var ex = await Assert.ThrowsAsync<UmbralDomainException>(
            () => _handler.Handle(
                new RotateOperatorPasswordCommand("user-1", new string('a', 129)), CancellationToken.None));

        Assert.Equal("operator_password_too_long", ex.Code);
    }

    [Fact]
    public async Task Handle_ValidRequest_RotatesViaPort_WithTrimmedValues()
    {
        var user = new OperatorUser("user-1", "jdoe", "jdoe@example.com", "John", "Doe", true);
        SetupExistingUser(user);

        var result = await _handler.Handle(
            new RotateOperatorPasswordCommand("  user-1  ", "  NewSecurePass1  "), CancellationToken.None);

        Assert.Equal(user, result);
        _portMock.Verify(
            p => p.RotateOperatorPasswordAsync("user-1", "NewSecurePass1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_SendsPasswordRotatedEmail()
    {
        var user = new OperatorUser("user-1", "jdoe", "jdoe@example.com", "John", "Doe", true);
        SetupExistingUser(user);

        await _handler.Handle(
            new RotateOperatorPasswordCommand("user-1", "NewSecurePass1"), CancellationToken.None);

        _emailMock.Verify(e => e.SendPasswordRotatedAsync(
            "jdoe@example.com", "jdoe", "NewSecurePass1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmailFails_StillReturnsOperator()
    {
        var user = new OperatorUser("user-1", "jdoe", "jdoe@example.com", "John", "Doe", true);
        SetupExistingUser(user);

        _emailMock.Setup(e => e.SendPasswordRotatedAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        var result = await _handler.Handle(
            new RotateOperatorPasswordCommand("user-1", "NewSecurePass1"), CancellationToken.None);

        // Assert — operator is returned despite email failure
        Assert.NotNull(result);
        Assert.Equal("user-1", result.Id);
    }
}
