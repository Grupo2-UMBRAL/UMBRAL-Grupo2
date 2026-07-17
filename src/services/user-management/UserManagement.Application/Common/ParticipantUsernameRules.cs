using System.Text.RegularExpressions;
using FluentValidation;

namespace UserManagement.Application.Common;

/// <summary>
/// The one definition of what a Participant username may be. Registration and renaming both apply
/// it, so a handle can never be acceptable to one and rejected by the other -- which would let a
/// player rename into something they could not have registered, or strand them on a handle they can
/// no longer re-submit.
/// </summary>
public static class ParticipantUsernameRules
{
    private const string UsernamePattern = "^[A-Za-z0-9._-]+$";

    public static IRuleBuilderOptions<T, string?> ParticipantUsername<T>(
        this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value => !string.IsNullOrWhiteSpace(value))
                .WithErrorCode("participant_username_required").WithMessage("Username is required.")
            .Must(value => value!.Trim().Length <= 40)
                .WithErrorCode("participant_username_too_long").WithMessage("Username must stay under 40 characters.")
            .Must(value => value!.Trim().Length >= 3)
                .WithErrorCode("participant_username_too_short").WithMessage("Username must contain at least 3 characters.")
            .Must(value => Regex.IsMatch(value!.Trim(), UsernamePattern))
                .WithErrorCode("participant_username_invalid")
                .WithMessage("Username only admits letters, digits, dot, underscore, or dash.");
}
