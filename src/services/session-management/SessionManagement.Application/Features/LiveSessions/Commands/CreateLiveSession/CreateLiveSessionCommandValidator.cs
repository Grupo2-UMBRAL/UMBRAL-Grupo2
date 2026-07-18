using FluentValidation;

namespace SessionManagement.Application.Features.LiveSessions;

public sealed class CreateLiveSessionCommandValidator : AbstractValidator<CreateLiveSessionCommand>
{
    public CreateLiveSessionCommandValidator()
    {
        RuleFor(command => command.MissionId).NotEmpty()
            .WithErrorCode("live_session_mission_required").WithMessage("LiveSession must reference a Mission.");
        RuleFor(command => command.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("live_session_name_required").WithMessage("LiveSession name is required.")
            .MaximumLength(120).WithErrorCode("live_session_name_required_too_long").WithMessage("LiveSession name cannot exceed 120 characters.");
        RuleFor(command => command.SelectedMissionStageIds).NotEmpty()
            .WithErrorCode("live_session_stage_flow_required").WithMessage("Select at least one active Play for Session Flow.");
        RuleForEach(command => command.SelectedMissionStageIds!).NotEmpty()
            .WithErrorCode("live_session_stage_required").WithMessage("Session Flow Play id is required.");
        RuleFor(command => command.SelectedMissionStageIds).Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithErrorCode("live_session_stage_flow_duplicate_stage").WithMessage("A Play cannot appear twice in Session Flow.");
    }
}
