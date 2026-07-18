using FluentValidation;

namespace SessionManagement.Application.Features.Penalties;

public sealed class ApplyPenaltyCommandValidator : AbstractValidator<ApplyPenaltyCommand>
{
    public ApplyPenaltyCommandValidator()
    {
        RuleFor(command => command.LiveSessionId).NotEmpty().WithErrorCode("live_session_id_required").WithMessage("LiveSession id is required.");
        RuleFor(command => command.SessionTeamId).NotEmpty().WithErrorCode("session_team_id_required").WithMessage("Session Team id is required.");
        RuleFor(command => command.CommandId).NotEmpty().WithErrorCode("penalty_command_id_required").WithMessage("Penalty command id is required.");
        RuleFor(command => command.Severity).Must(BeKnownSeverity).WithErrorCode("penalty_severity_invalid").WithMessage("Penalty severity is invalid.");
        RuleFor(command => command.Reason).NotEmpty().WithErrorCode("penalty_reason_required").WithMessage("Penalty reason is required.");
    }

    private static bool BeKnownSeverity(string? severity) => severity is "Minor" or "Major" or "Critical";
}
