using FluentValidation;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed class GenerateJoinCodeCommandValidator : AbstractValidator<GenerateJoinCodeCommand>
{
    public GenerateJoinCodeCommandValidator() => RuleFor(command => command.LiveSessionId)
        .NotEmpty().WithErrorCode("live_session_id_required").WithMessage("LiveSession id is required.");
}
