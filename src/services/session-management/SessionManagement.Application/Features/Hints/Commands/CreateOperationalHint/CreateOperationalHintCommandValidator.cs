using FluentValidation;

namespace SessionManagement.Application.Features.Hints;

public sealed class CreateOperationalHintCommandValidator : AbstractValidator<CreateOperationalHintCommand>
{
    public CreateOperationalHintCommandValidator()
    {
        RuleFor(command => command.LiveSessionId).NotEmpty().WithErrorCode("live_session_id_required").WithMessage("LiveSession id is required.");
        RuleFor(command => command.MissionStageId).NotEmpty().WithErrorCode("mission_stage_id_required").WithMessage("Play id is required.");
        RuleFor(command => command.Content).NotEmpty().WithErrorCode("live_session_stage_hint_content_required").WithMessage("LiveSession stage hint content is required.");
        RuleFor(command => command).Must(command => command.Latitude.HasValue == command.Longitude.HasValue)
            .WithErrorCode("live_session_stage_hint_coordinates_incomplete").WithMessage("Operational Hint coordinates must include latitude and longitude together.");
        RuleFor(command => command.Latitude).Must(BeFinite).When(command => command.Latitude.HasValue)
            .WithErrorCode("live_session_stage_hint_coordinate_invalid").WithMessage("Operational Hint coordinates must be finite numbers.");
        RuleFor(command => command.Longitude).Must(BeFinite).When(command => command.Longitude.HasValue)
            .WithErrorCode("live_session_stage_hint_coordinate_invalid").WithMessage("Operational Hint coordinates must be finite numbers.");
    }

    private static bool BeFinite(double? value) => value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value);
}
