using FluentValidation;

namespace UserManagement.Application.Features.Operators.Commands.ResendOperatorInvitation;

/// <summary>
/// FluentValidation validator for <see cref="ResendOperatorInvitationCommand"/>: the operator user id
/// is required. Surfaced by <c>UmbralValidationBehavior</c> as code <c>operator_user_id_required</c>.
/// </summary>
public sealed class ResendOperatorInvitationCommandValidator : AbstractValidator<ResendOperatorInvitationCommand>
{
    public ResendOperatorInvitationCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.UserId)
            .Must(v => !string.IsNullOrWhiteSpace(v))
                .WithErrorCode("operator_user_id_required").WithMessage("Operator user id is required.");
    }
}
