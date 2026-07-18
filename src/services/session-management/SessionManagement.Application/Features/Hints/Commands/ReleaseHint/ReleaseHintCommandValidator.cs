using FluentValidation;

namespace SessionManagement.Application.Features.Hints;

public sealed class ReleaseHintCommandValidator : AbstractValidator<ReleaseHintCommand>
{
    public ReleaseHintCommandValidator()
    {
        RuleFor(command => command.LiveSessionId).NotEmpty().WithErrorCode("live_session_id_required").WithMessage("LiveSession id is required.");
        RuleFor(command => command.HintId).NotEmpty().WithErrorCode("hint_id_required").WithMessage("Hint id is required.");
    }
}
