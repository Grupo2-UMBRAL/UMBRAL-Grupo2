using FluentValidation;
using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed class RegisterTeamByOperatorCommandValidator : AbstractValidator<RegisterTeamByOperatorCommand>
{
    public RegisterTeamByOperatorCommandValidator()
    {
        RuleFor(command => command.LiveSessionId).NotEmpty()
            .WithErrorCode("live_session_id_required").WithMessage("LiveSession id is required.");
        RuleFor(command => command.TeamName).Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("session_team_name_required").WithMessage("Session Team name is required.")
            .MaximumLength(SessionTeam.NameMaximumLength).WithErrorCode("session_team_name_too_long").WithMessage("Session Team name cannot exceed 80 characters.");
    }
}
