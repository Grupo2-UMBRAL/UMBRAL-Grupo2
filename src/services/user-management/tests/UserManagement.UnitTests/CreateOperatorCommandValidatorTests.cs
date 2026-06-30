using Umbral.ServiceDefaults;
using UserManagement.Application.Features.Operators.Commands.CreateOperator;
using Xunit;

namespace UserManagement.UnitTests;

public sealed class CreateOperatorCommandValidatorTests
{
    private static CreateOperatorCommand Valid() =>
        new("jdoe", "jdoe@example.com", "John", "Doe", "SecurePass123!");

    [Fact]
    public void Validate_AcceptsWellFormedCommand()
    {
        // No exception == valid.
        CreateOperatorCommandValidator.Validate(Valid());
    }

    [Theory]
    [InlineData("bad name", "operator_username_invalid")]   // space not allowed
    [InlineData("nope!", "operator_username_invalid")]      // '!' not allowed
    [InlineData("", "operator_username_required")]
    public void Validate_RejectsInvalidUsername(string username, string expectedCode)
    {
        var command = Valid() with { Username = username };

        var ex = Assert.Throws<UmbralDomainException>(() => CreateOperatorCommandValidator.Validate(command));

        Assert.Equal(expectedCode, ex.Code);
        Assert.Equal(UmbralFailureCategory.Validation, ex.Category);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@dot")]
    [InlineData("@example.com")]
    public void Validate_RejectsMalformedEmail(string email)
    {
        var command = Valid() with { Email = email };

        var ex = Assert.Throws<UmbralDomainException>(() => CreateOperatorCommandValidator.Validate(command));

        Assert.Equal("operator_email_invalid", ex.Code);
    }

    [Fact]
    public void Validate_RejectsShortPassword()
    {
        var command = Valid() with { Password = "short" };

        var ex = Assert.Throws<UmbralDomainException>(() => CreateOperatorCommandValidator.Validate(command));

        Assert.Equal("operator_password_too_short", ex.Code);
    }

    [Fact]
    public void Validate_RejectsBlankFirstName()
    {
        var command = Valid() with { FirstName = "   " };

        var ex = Assert.Throws<UmbralDomainException>(() => CreateOperatorCommandValidator.Validate(command));

        Assert.Equal("operator_first_name_required", ex.Code);
    }
}
