using FluentValidation;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed class CloseEnrollmentWindowCommandValidator : AbstractValidator<CloseEnrollmentWindowCommand>
{
    public CloseEnrollmentWindowCommandValidator() => RuleFor(command => command.LiveSessionId)
        .NotEmpty().WithErrorCode("live_session_id_required").WithMessage("LiveSession id is required.");
}
