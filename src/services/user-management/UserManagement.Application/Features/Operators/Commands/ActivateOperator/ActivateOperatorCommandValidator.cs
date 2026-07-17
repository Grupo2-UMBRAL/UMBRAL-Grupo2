using FluentValidation;

namespace UserManagement.Application.Features.Operators.Commands.ActivateOperator;

public sealed class ActivateOperatorCommandValidator : AbstractValidator<ActivateOperatorCommand>
{
    public ActivateOperatorCommandValidator()
    {
        RuleFor(command => command.UserId)
            .Must(userId => !string.IsNullOrWhiteSpace(userId))
            .WithErrorCode("operator_user_id_required")
            .WithMessage("Operator user id is required.");
    }
}
