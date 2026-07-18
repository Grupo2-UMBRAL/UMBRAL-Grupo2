using FluentValidation;
using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed class JoinSessionTeamCommandValidator : AbstractValidator<JoinSessionTeamCommand>
{
    public JoinSessionTeamCommandValidator()
    {
        RuleFor(command => command.JoinCode).Must(BeAJoinCode)
            .WithErrorCode("join_code_required").WithMessage("Join Code is required.");
        RuleFor(command => command.SessionTeamId).NotEmpty()
            .WithErrorCode("session_team_id_required").WithMessage("Session Team id is required.");
    }

    private static bool BeAJoinCode(string? value)
    {
        try { JoinCode.Parse(value); return true; }
        catch { return false; }
    }
}
