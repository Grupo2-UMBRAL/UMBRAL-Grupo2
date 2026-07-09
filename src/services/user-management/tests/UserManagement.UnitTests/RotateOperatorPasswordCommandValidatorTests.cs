using UserManagement.Application.Features.Operators.Commands.RotateOperatorPassword;
using Xunit;

namespace UserManagement.UnitTests;

public sealed class RotateOperatorPasswordCommandValidatorTests
{
    private static readonly RotateOperatorPasswordCommandValidator Validator = new();

    private static string FirstErrorCode(RotateOperatorPasswordCommand command)
    {
        var result = Validator.Validate(command);
        Assert.False(result.IsValid);
        return result.Errors[0].ErrorCode;
    }

    [Fact]
    public void Validate_AcceptsWellFormedCommand()
    {
        Assert.True(Validator.Validate(new RotateOperatorPasswordCommand("user-1", "ValidPass123")).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsBlankUserId(string userId)
    {
        Assert.Equal("operator_user_id_required", FirstErrorCode(new RotateOperatorPasswordCommand(userId, "ValidPass123")));
    }

    [Theory]
    [InlineData(null, "operator_password_required")]
    [InlineData("   ", "operator_password_required")]
    [InlineData("short", "operator_password_too_short")]
    public void Validate_RejectsInvalidPassword(string? password, string expectedCode)
    {
        Assert.Equal(expectedCode, FirstErrorCode(new RotateOperatorPasswordCommand("user-1", password)));
    }

    [Fact]
    public void Validate_RejectsPasswordOver128Chars()
    {
        Assert.Equal(
            "operator_password_too_long",
            FirstErrorCode(new RotateOperatorPasswordCommand("user-1", new string('a', 129))));
    }
}
