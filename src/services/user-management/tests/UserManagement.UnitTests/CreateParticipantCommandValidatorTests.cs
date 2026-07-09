using UserManagement.Application.Features.Participants.Commands.CreateParticipant;
using Xunit;

namespace UserManagement.UnitTests;

public sealed class CreateParticipantCommandValidatorTests
{
    private static readonly CreateParticipantCommandValidator Validator = new();

    private static CreateParticipantCommand Valid() =>
        new("player1", "player1@example.com", "SecurePass123!");

    private static string FirstErrorCode(CreateParticipantCommand command)
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
    [InlineData("", "participant_username_required")]
    [InlineData("ab", "participant_username_too_short")]
    [InlineData("bad name", "participant_username_invalid")]
    public void Validate_RejectsInvalidUsername(string username, string expectedCode)
    {
        Assert.Equal(expectedCode, FirstErrorCode(Valid() with { Username = username }));
    }

    [Fact]
    public void Validate_RejectsMalformedEmail()
    {
        Assert.Equal("participant_email_invalid", FirstErrorCode(Valid() with { Email = "not-an-email" }));
    }

    [Fact]
    public void Validate_RejectsShortPassword()
    {
        Assert.Equal("participant_password_too_short", FirstErrorCode(Valid() with { Password = "short" }));
    }
}
