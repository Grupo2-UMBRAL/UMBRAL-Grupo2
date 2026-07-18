using FluentValidation;

namespace SessionManagement.Application.Features.EvidenceSubmissions;

public sealed class SubmitTriviaAnswerCommandValidator : AbstractValidator<SubmitTriviaAnswerCommand>
{
    public SubmitTriviaAnswerCommandValidator()
    {
        RuleFor(command => command.SessionTeamId).NotEmpty().WithErrorCode("session_team_id_required").WithMessage("Session Team id is required.");
        RuleFor(command => command.SelectedChoiceId).NotEmpty().WithErrorCode("selected_choice_id_required").WithMessage("Selected choice id is required.");
    }
}
