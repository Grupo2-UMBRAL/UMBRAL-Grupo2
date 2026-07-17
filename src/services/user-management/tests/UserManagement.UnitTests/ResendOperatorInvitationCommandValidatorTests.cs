using UserManagement.Application.Features.Operators.Commands.ResendOperatorInvitation;
using Xunit;

namespace UserManagement.UnitTests;

public sealed class ResendOperatorInvitationCommandValidatorTests
{
    private static readonly ResendOperatorInvitationCommandValidator Validator = new();

    [Fact]
    public void Validate_AcceptsNonBlankUserId()
    {
        Assert.True(Validator.Validate(new ResendOperatorInvitationCommand("user-1")).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsBlankUserId(string userId)
    {
        var result = Validator.Validate(new ResendOperatorInvitationCommand(userId));

        Assert.False(result.IsValid);
        Assert.Equal("operator_user_id_required", result.Errors[0].ErrorCode);
    }
}
