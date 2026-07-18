using SessionManagement.Domain.LiveSessions;
using MediatR;

using SessionManagement.Application.Abstractions.Scoring;
using SessionManagement.Application.Abstractions.Realtime;
using SessionManagement.Application.Features.SessionSnapshots;
using SessionManagement.Application.Abstractions;
using SessionManagement.Application.Features.SessionEnrollment;
using Umbral.ServiceDefaults;

namespace SessionManagement.Application.Features.EvidenceSubmissions;

public sealed class SubmitEvidenceCommandHandler(
    ILiveSessionRepository liveSessionRepository,
    TimeProvider timeProvider,
    ICurrentParticipantIdentity currentParticipantIdentity,
    ISessionRealtimeNotifier realtimeNotifier,
    IScoringMonitoringClient scoringAuditClient)
    : EvidenceSubmissionFlowHandler<SubmitEvidenceCommand, SubmitEvidenceResponse>(
        liveSessionRepository, timeProvider, realtimeNotifier, scoringAuditClient)
{
    protected override async Task<LiveSession?> GetLiveSessionAsync(SubmitEvidenceCommand request, ILiveSessionRepository repository, CancellationToken cancellationToken)
    {
        return await repository.GetBySessionTeamIdWithEvidenceSubmissionsAsync(request.SessionTeamId, cancellationToken);
    }

    protected override Exception CreateNotFoundException(SubmitEvidenceCommand request)
    {
        return new UmbralDomainException(
            "session_team_not_found",
            $"Session Team '{request.SessionTeamId}' was not found.",
            UmbralFailureCategory.NotFound);
    }

    protected override Guid GetSessionTeamId(SubmitEvidenceCommand request, LiveSession session)
        => request.SessionTeamId;

    protected override void EnsurePermissions(SubmitEvidenceCommand request, LiveSession session)
    {
        var participantUserId = currentParticipantIdentity.GetRequiredParticipantUserId();
        EnsureParticipantBelongsToSessionTeam(session, request.SessionTeamId, participantUserId.Value);
    }

    protected override Task<DomainStepResult> ExecuteDomainStepAsync(SubmitEvidenceCommand request, LiveSession session, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
    {
        var evidenceSubmission = session.SubmitEvidence(
            request.SessionTeamId,
            request.QrHash,
            occurredAtUtc);

        var result = new DomainStepResult(
            EvidenceSubmission: evidenceSubmission,
            PublishOutcomeChanged: false,
            PreviousOutcome: null,
            OutcomeChangedReason: null,
            RecordStageCredit: evidenceSubmission.Outcome == ValidationOutcome.Accepted,
            IsValidationOverride: false,
            PublishProgressChanged: evidenceSubmission.Outcome == ValidationOutcome.Accepted,
            ProgressChangedReason: "Evidence accepted; Session Team progression changed.",
            StateChangedReason: "LiveSession finalized after Evidence Submission."
        );

        return Task.FromResult(result);
    }

    protected override SubmitEvidenceResponse CreateResponse(SubmitEvidenceCommand request, LiveSession session, EvidenceSubmission evidenceSubmission, DomainStepResult domainResult, LiveSessionStage? currentStage, string progressState, DateTimeOffset occurredAtUtc)
    {
        return new SubmitEvidenceResponse(
            session.Id,
            request.SessionTeamId,
            evidenceSubmission.Id,
            evidenceSubmission.Outcome.ToString(),
            progressState,
            MapCurrentStage(currentStage),
            session.SequenceNumber,
            occurredAtUtc);
    }
}
