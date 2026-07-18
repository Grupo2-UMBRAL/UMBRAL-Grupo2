using FluentValidation;

namespace SessionManagement.Application.Features.LiveSessions;

public sealed class DeactivateStageCommandValidator : AbstractValidator<DeactivateStageCommand>
{
    public DeactivateStageCommandValidator()
    {
        RuleFor(command => command.LiveSessionId).NotEmpty().WithErrorCode("live_session_id_required").WithMessage("LiveSession id is required.");
        RuleFor(command => command.MissionStageId).NotEmpty().WithErrorCode("mission_stage_id_required").WithMessage("Play id is required.");
    }
}
