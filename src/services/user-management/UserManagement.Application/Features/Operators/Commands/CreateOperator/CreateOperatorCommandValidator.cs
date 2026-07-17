using System.Text.RegularExpressions;
using FluentValidation;

namespace UserManagement.Application.Features.Operators.Commands.CreateOperator;

/// <summary>
/// FluentValidation validator for <see cref="CreateOperatorCommand"/>. Fail-fast (CascadeMode.Stop)
/// so the first failing rule, in declared order, is the one surfaced by
/// <c>UmbralValidationBehavior</c> — preserving the exact <c>operator_*</c> codes and HTTP contract.
/// Only username and email are collected now: the Operator sets their own password and completes
/// their profile through the Keycloak onboarding invitation, so there is nothing else to validate.
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
    }
}
