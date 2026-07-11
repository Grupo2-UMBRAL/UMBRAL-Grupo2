using SessionManagement.Domain.LiveSessions;
using MediatR;

using SessionManagement.Application.Abstractions.Scoring;
using SessionManagement.Application.Abstractions.Realtime;
using SessionManagement.Application.Features.SessionSnapshots;
using SessionManagement.Application.Abstractions;
using Umbral.ServiceDefaults;

namespace SessionManagement.Application.Features.EvidenceSubmissions;

public sealed class OverrideValidationOutcomeHandler(
    IUnitOfWork unitOfWork, 
    ILiveSessionRepository liveSessionRepository,
    TimeProvider timeProvider,
    ICurrentOperatorIdentity currentOperatorIdentity,
    ISessionRealtimeNotifier realtimeNotifier,
    IScoringMonitoringClient scoringAuditClient)
    : EvidenceSubmissionFlowHandler<OverrideValidationOutcomeCommand, OverrideValidationOutcomeResponse>(
        unitOfWork, liveSessionRepository, timeProvider, realtimeNotifier, scoringAuditClient)
{
    protected override async Task<LiveSession?> GetLiveSessionAsync(OverrideValidationOutcomeCommand request, ILiveSessionRepository repository, CancellationToken cancellationToken)
    {
        return await repository.GetByEvidenceSubmissionIdWithSubmissionsAndLogsAsync(request.EvidenceSubmissionId, cancellationToken);
    }

    protected override Exception CreateNotFoundException(OverrideValidationOutcomeCommand request)
    {
        return new UmbralDomainException(
            "evidence_submission_not_found",
            $"Evidence Submission '{request.EvidenceSubmissionId}' was not found.",
            UmbralFailureCategory.NotFound);
    }

    protected override Guid GetSessionTeamId(OverrideValidationOutcomeCommand request, LiveSession session)
    {
        return session.EvidenceSubmissions.Single(s => s.Id == request.EvidenceSubmissionId).SessionTeamId;
    }

    protected override void EnsurePermissions(OverrideValidationOutcomeCommand request, LiveSession session)
    {
        // Identity validation happens at the Controller level (authentication/authorization)
        // We defer extracting the specific operator user ID until the domain step.
    }

    protected override Task<DomainStepResult> ExecuteDomainStepAsync(OverrideValidationOutcomeCommand request, LiveSession session, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
    {
        var evidenceSubmission = session.EvidenceSubmissions.Single(submission => submission.Id == request.EvidenceSubmissionId);
        var previousOutcome = evidenceSubmission.Outcome.ToString();
        var previousProgressState = session.GetProgressStateForTeam(evidenceSubmission.SessionTeamId);
        var previousStage = session.GetCurrentStageForTeam(evidenceSubmission.SessionTeamId);

        var operatorUserId = currentOperatorIdentity.GetRequiredOperatorUserId();
        
        var validationOverrideLog = session.OverrideValidationOutcome(
            request.EvidenceSubmissionId,
            operatorUserId,
            request.IsAccepted,
            request.Reason,
            occurredAtUtc);

        var currentStage = session.GetCurrentStageForTeam(evidenceSubmission.SessionTeamId);
        var progressState = session.GetProgressStateForTeam(evidenceSubmission.SessionTeamId);

        var recordStageCredit = !string.Equals(previousOutcome, ValidationOutcome.Accepted.ToString(), StringComparison.Ordinal)
            && evidenceSubmission.Outcome == ValidationOutcome.Accepted;

        var publishProgressChanged = !string.Equals(previousProgressState, progressState, StringComparison.Ordinal)
            || previousStage?.MissionStageId != currentStage?.MissionStageId;

        var result = new DomainStepResult(
            EvidenceSubmission: evidenceSubmission,
            PublishOutcomeChanged: true,
            PreviousOutcome: previousOutcome,
            OutcomeChangedReason: "Validation Override changed Evidence Submission outcome.",
            RecordStageCredit: recordStageCredit,
            IsValidationOverride: true,
            PublishProgressChanged: publishProgressChanged,
            ProgressChangedReason: "Validation Override accepted Evidence Submission; Session Team progression changed.",
            StateChangedReason: "LiveSession finalized after Validation Override.",
            ValidationOverrideLogId: validationOverrideLog.Id,
            StageCreditRecordedAtUtc: occurredAtUtc,
            RecordStageCreditBeforeOutcomeChanged: true
        );

        return Task.FromResult(result);
    }

    protected override OverrideValidationOutcomeResponse CreateResponse(OverrideValidationOutcomeCommand request, LiveSession session, EvidenceSubmission evidenceSubmission, DomainStepResult domainResult, LiveSessionStage? currentStage, string progressState, DateTimeOffset occurredAtUtc)
    {
        return new OverrideValidationOutcomeResponse(
            session.Id,
            evidenceSubmission.Id,
            domainResult.ValidationOverrideLogId,
            evidenceSubmission.SessionTeamId,
            evidenceSubmission.MissionStageId,
            domainResult.PreviousOutcome ?? string.Empty,
            evidenceSubmission.Outcome.ToString(),
            progressState,
            MapCurrentStage(currentStage),
            session.SequenceNumber,
            occurredAtUtc);
    }
}
