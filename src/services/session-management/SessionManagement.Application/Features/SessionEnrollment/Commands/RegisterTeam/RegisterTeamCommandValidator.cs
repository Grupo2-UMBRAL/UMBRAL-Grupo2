using FluentValidation;
using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed class RegisterTeamCommandValidator : AbstractValidator<RegisterTeamCommand>
{
    public RegisterTeamCommandValidator()
    {
        RuleFor(command => command.JoinCode).Must(BeAJoinCode)
            .WithErrorCode("join_code_required").WithMessage("Join Code is required.");
        RuleFor(command => command.TeamName).Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("session_team_name_required").WithMessage("Session Team name is required.")
            .MaximumLength(SessionTeam.NameMaximumLength).WithErrorCode("session_team_name_too_long").WithMessage("Session Team name cannot exceed 80 characters.");
    }

    private static bool BeAJoinCode(string? value)
    {
        try { JoinCode.Parse(value); return true; }
        catch { return false; }
    }
}
