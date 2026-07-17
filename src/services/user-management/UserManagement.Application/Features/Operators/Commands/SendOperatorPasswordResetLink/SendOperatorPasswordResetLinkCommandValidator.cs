using FluentValidation;

namespace UserManagement.Application.Features.Operators.Commands.SendOperatorPasswordResetLink;

public sealed class SendOperatorPasswordResetLinkCommandValidator : AbstractValidator<SendOperatorPasswordResetLinkCommand>
{
    public SendOperatorPasswordResetLinkCommandValidator()
    {
        RuleFor(command => command.UserId)
            .Must(userId => !string.IsNullOrWhiteSpace(userId))
            .WithErrorCode("operator_user_id_required")
            .WithMessage("Operator user id is required.");
    }
}
