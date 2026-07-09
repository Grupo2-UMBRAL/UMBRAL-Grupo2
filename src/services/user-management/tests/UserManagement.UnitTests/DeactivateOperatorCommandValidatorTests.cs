using UserManagement.Application.Features.Operators.Commands.DeactivateOperator;
using Xunit;

namespace UserManagement.UnitTests;

public sealed class DeactivateOperatorCommandValidatorTests
{
    private static readonly DeactivateOperatorCommandValidator Validator = new();

    [Fact]
    public void Validate_AcceptsNonBlankUserId()
    {
        Assert.True(Validator.Validate(new DeactivateOperatorCommand("user-1")).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsBlankUserId(string userId)
    {
        var result = Validator.Validate(new DeactivateOperatorCommand(userId));

        Assert.False(result.IsValid);
        Assert.Equal("operator_user_id_required", result.Errors[0].ErrorCode);
    }
}
