using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;
using Umbral.ServiceDefaults;
using UserManagement.Application.Common;

namespace UserManagement.Application.Features.Participants.Commands.CreateParticipant;

/// <summary>
/// FluentValidation validator for <see cref="CreateParticipantCommand"/>. Fail-fast (CascadeMode.Stop)
/// so the first failing rule in declared order is surfaced by <c>UmbralValidationBehavior</c>,
/// preserving the exact <c>participant_*</c> codes. Password rules delegate to <see cref="PasswordPolicy"/>.
/// </summary>
public sealed class CreateParticipantCommandValidator : AbstractValidator<CreateParticipantCommand>
{
    private const string UsernamePattern = "^[A-Za-z0-9._-]+$";
    private const string EmailPattern = @"^[^\s@]+@[^\s@]+\.[^\s@]+$";

    public CreateParticipantCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Username)
            .Must(v => !string.IsNullOrWhiteSpace(v))
                .WithErrorCode("participant_username_required").WithMessage("Username is required.")
            .Must(v => v!.Trim().Length <= 40)
                .WithErrorCode("participant_username_too_long").WithMessage("Username must stay under 40 characters.")
            .Must(v => v!.Trim().Length >= 3)
                .WithErrorCode("participant_username_too_short").WithMessage("Username must contain at least 3 characters.")
            .Must(v => Regex.IsMatch(v!.Trim(), UsernamePattern))
                .WithErrorCode("participant_username_invalid")
                .WithMessage("Username only admits letters, digits, dot, underscore, or dash.");

        RuleFor(x => x.Email)
            .Must(v => !string.IsNullOrWhiteSpace(v))
                .WithErrorCode("participant_email_required").WithMessage("Email is required.")
            .Must(v => v!.Trim().Length <= 120)
                .WithErrorCode("participant_email_too_long").WithMessage("Email must stay under 120 characters.")
            .Must(v => Regex.IsMatch(v!.Trim(), EmailPattern))
                .WithErrorCode("participant_email_invalid").WithMessage("Email format is invalid.");

        RuleFor(x => x.Password).Custom(ValidatePassword);
    }

    private static void ValidatePassword(string? value, ValidationContext<CreateParticipantCommand> context)
    {
        try
        {
            PasswordPolicy.Normalize(value, "participant_password");
        }
        catch (UmbralServiceException exception)
        {
            context.AddFailure(new ValidationFailure(context.PropertyPath, exception.Message)
            {
                ErrorCode = exception.Code
            });
        }
    }
}
