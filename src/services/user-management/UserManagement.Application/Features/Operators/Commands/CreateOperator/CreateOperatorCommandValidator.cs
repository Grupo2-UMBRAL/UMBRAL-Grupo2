using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;
using Umbral.ServiceDefaults;
using UserManagement.Application.Common;

namespace UserManagement.Application.Features.Operators.Commands.CreateOperator;

/// <summary>
/// FluentValidation validator for <see cref="CreateOperatorCommand"/>. Fail-fast (CascadeMode.Stop)
/// so the first failing rule, in declared order, is the one surfaced by
/// <c>UmbralValidationBehavior</c> — preserving the exact <c>operator_*</c> codes and HTTP contract
/// of the previous manual validator. Password rules delegate to the shared <see cref="PasswordPolicy"/>.
/// </summary>
public sealed class CreateOperatorCommandValidator : AbstractValidator<CreateOperatorCommand>
{
    private const string UsernamePattern = "^[A-Za-z0-9._-]+$";
    private const string EmailPattern = @"^[^\s@]+@[^\s@]+\.[^\s@]+$";

    public CreateOperatorCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Username)
            .Must(v => !string.IsNullOrWhiteSpace(v))
                .WithErrorCode("operator_username_required").WithMessage("Username is required.")
            .Must(v => v!.Trim().Length <= 40)
                .WithErrorCode("operator_username_too_long").WithMessage("Username must stay under 40 characters.")
            .Must(v => Regex.IsMatch(v!.Trim(), UsernamePattern))
                .WithErrorCode("operator_username_invalid")
                .WithMessage("Username only admits letters, digits, dot, underscore, or dash.");

        RuleFor(x => x.Email)
            .Must(v => !string.IsNullOrWhiteSpace(v))
                .WithErrorCode("operator_email_required").WithMessage("Email is required.")
            .Must(v => v!.Trim().Length <= 120)
                .WithErrorCode("operator_email_too_long").WithMessage("Email must stay under 120 characters.")
            .Must(v => Regex.IsMatch(v!.Trim(), EmailPattern))
                .WithErrorCode("operator_email_invalid").WithMessage("Email format is invalid.");

        RuleFor(x => x.FirstName)
            .Must(v => !string.IsNullOrWhiteSpace(v))
                .WithErrorCode("operator_first_name_required").WithMessage("First name is required.")
            .Must(v => v!.Trim().Length <= 80)
                .WithErrorCode("operator_first_name_too_long").WithMessage("First name must stay under 80 characters.");

        RuleFor(x => x.LastName)
            .Must(v => !string.IsNullOrWhiteSpace(v))
                .WithErrorCode("operator_last_name_required").WithMessage("Last name is required.")
            .Must(v => v!.Trim().Length <= 80)
                .WithErrorCode("operator_last_name_too_long").WithMessage("Last name must stay under 80 characters.");

        RuleFor(x => x.Password).Custom(ValidatePassword);
    }

    /// <summary>Runs the shared password policy and translates its categorized failure into a rule error.</summary>
    private static void ValidatePassword(string? value, ValidationContext<CreateOperatorCommand> context)
    {
        try
        {
            PasswordPolicy.Normalize(value, "operator_password");
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
