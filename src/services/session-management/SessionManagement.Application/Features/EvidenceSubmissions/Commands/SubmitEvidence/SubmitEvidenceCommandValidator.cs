using FluentValidation;
using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.Application.Features.EvidenceSubmissions;

public sealed class SubmitEvidenceCommandValidator : AbstractValidator<SubmitEvidenceCommand>
{
    public SubmitEvidenceCommandValidator()
    {
        RuleFor(command => command.SessionTeamId).NotEmpty().WithErrorCode("session_team_id_required").WithMessage("Session Team id is required.");
        RuleFor(command => command.QrHash).Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("evidence_submission_hash_required").WithMessage("Evidence Submission QR hash is required.")
            .MaximumLength(EvidenceSubmission.SubmittedHashMaximumLength).WithErrorCode("evidence_submission_hash_required_too_long").WithMessage("Evidence Submission QR hash is too long.");
    }
}
