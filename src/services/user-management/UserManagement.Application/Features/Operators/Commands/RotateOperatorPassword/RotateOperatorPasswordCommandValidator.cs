using FluentValidation;
using FluentValidation.Results;
using Umbral.ServiceDefaults;
using UserManagement.Application.Common;

namespace UserManagement.Application.Features.Operators.Commands.RotateOperatorPassword;

/// <summary>
/// FluentValidation validator for <see cref="RotateOperatorPasswordCommand"/>: a non-blank user id
/// and the shared <see cref="PasswordPolicy"/> rules (codes <c>operator_password_*</c>, identical to
/// create-operator). Fail-fast, surfaced by <c>UmbralValidationBehavior</c>.
/// </summary>
public sealed class RotateOperatorPasswordCommandValidator : AbstractValidator<RotateOperatorPasswordCommand>
{
    public RotateOperatorPasswordCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.UserId)
            .Must(v => !string.IsNullOrWhiteSpace(v))
                .WithErrorCode("operator_user_id_required").WithMessage("Operator user id is required.");

        RuleFor(x => x.Password).Custom(ValidatePassword);
    }

    private static void ValidatePassword(string? value, ValidationContext<RotateOperatorPasswordCommand> context)
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
