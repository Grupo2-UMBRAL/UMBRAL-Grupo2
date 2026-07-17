using UserManagement.Application.Features.Operators.Commands.CreateOperator;
using Xunit;

namespace UserManagement.UnitTests;

public sealed class CreateOperatorCommandValidatorTests
{
    private static readonly CreateOperatorCommandValidator Validator = new();

    private static CreateOperatorCommand Valid() =>
        new("jdoe", "jdoe@example.com");

    // CascadeMode.Stop -> a failing command yields exactly one error: the first rule that failed,
    // in declared order. That single ErrorCode is what UmbralValidationBehavior surfaces to the client.
    private static string FirstErrorCode(CreateOperatorCommand command)
    {
        var result = Validator.Validate(command);
        Assert.False(result.IsValid);
        return result.Errors[0].ErrorCode;
    }

    [Fact]
    public void Validate_AcceptsWellFormedCommand()
    {
        Assert.True(Validator.Validate(Valid()).IsValid);
    }

    [Theory]
    [InlineData("bad name", "operator_username_invalid")]   // space not allowed
    [InlineData("nope!", "operator_username_invalid")]      // '!' not allowed
    [InlineData("", "operator_username_required")]
    public void Validate_RejectsInvalidUsername(string username, string expectedCode)
    {
        Assert.Equal(expectedCode, FirstErrorCode(Valid() with { Username = username }));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@dot")]
    [InlineData("@example.com")]
    public void Validate_RejectsMalformedEmail(string email)
    {
        Assert.Equal("operator_email_invalid", FirstErrorCode(Valid() with { Email = email }));
    }

    [Fact]
    public void Validate_RejectsBlankEmail()
    {
        Assert.Equal("operator_email_required", FirstErrorCode(Valid() with { Email = "   " }));
    }
}
