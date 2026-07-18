using FluentValidation;
using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.Application.Features.EvidenceSubmissions;

public sealed class OverrideValidationOutcomeCommandValidator : AbstractValidator<OverrideValidationOutcomeCommand>
{
    public OverrideValidationOutcomeCommandValidator()
    {
        RuleFor(command => command.EvidenceSubmissionId).NotEmpty().WithErrorCode("evidence_submission_id_required").WithMessage("Evidence Submission id is required.");
        RuleFor(command => command.Reason).Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("validation_override_reason_required").WithMessage("Validation Override reason is required.")
            .MaximumLength(ValidationOverrideLog.ReasonMaximumLength).WithErrorCode("validation_override_reason_required_too_long").WithMessage("Validation Override reason is too long.");
    }
}
