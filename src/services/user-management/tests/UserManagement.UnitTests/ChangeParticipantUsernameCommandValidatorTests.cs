using UserManagement.Application.Features.Participants.Commands.ChangeParticipantUsername;
using Xunit;

namespace UserManagement.UnitTests;

/// <summary>
/// The rules themselves are proven by <see cref="CreateParticipantCommandValidatorTests"/>; both
/// validators share one rule set. What matters here is that renaming really is held to the same bar
/// registration is, under the same codes -- a username accepted here but rejected there (or vice
/// versa) would let a player rename into a handle they could never have registered.
/// </summary>
public sealed class ChangeParticipantUsernameCommandValidatorTests
{
    private static readonly ChangeParticipantUsernameCommandValidator Validator = new();

    [Fact]
    public void Validate_AcceptsWellFormedUsername()
    {
        Assert.True(Validator.Validate(new ChangeParticipantUsernameCommand("player1")).IsValid);
    }

    [Theory]
    [InlineData("", "participant_username_required")]
    [InlineData("ab", "participant_username_too_short")]
    [InlineData("bad name", "participant_username_invalid")]
    public void Validate_RejectsInvalidUsername_WithTheRegistrationCodes(string username, string expectedCode)
    {
        var result = Validator.Validate(new ChangeParticipantUsernameCommand(username));

        Assert.False(result.IsValid);
        Assert.Equal(expectedCode, result.Errors[0].ErrorCode);
    }
}
