using FluentValidation;

namespace SessionManagement.Application.Features.SessionLifecycle;

public sealed class TransitionLiveSessionStateCommandValidator : AbstractValidator<TransitionLiveSessionStateCommand>
{
    public TransitionLiveSessionStateCommandValidator()
    {
        RuleFor(command => command.LiveSessionId).NotEmpty().WithErrorCode("live_session_id_required").WithMessage("LiveSession id is required.");
        RuleFor(command => command.Action).IsInEnum().WithErrorCode("live_session_lifecycle_action_invalid").WithMessage("LiveSession lifecycle action is invalid.");
    }
}
