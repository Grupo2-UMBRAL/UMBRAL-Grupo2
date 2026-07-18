using SessionManagement.Domain.LiveSessions;
using MediatR;

using SessionManagement.Application.Abstractions.Scoring;
using SessionManagement.Application.Abstractions.Realtime;
using SessionManagement.Application.Features.SessionSnapshots;
using SessionManagement.Application.Abstractions;
using SessionManagement.Application.Features.SessionEnrollment;
using Umbral.ServiceDefaults;

namespace SessionManagement.Application.Features.EvidenceSubmissions;

public sealed class SubmitTriviaAnswerHandler(
    ILiveSessionRepository liveSessionRepository,
    TimeProvider timeProvider,
    ICurrentParticipantIdentity currentParticipantIdentity,
    ISessionRealtimeNotifier realtimeNotifier,
    IScoringMonitoringClient scoringAuditClient)
    : EvidenceSubmissionFlowHandler<SubmitTriviaAnswerCommand, SubmitEvidenceResponse>(
        liveSessionRepository, timeProvider, realtimeNotifier, scoringAuditClient)
{
    protected override async Task<LiveSession?> GetLiveSessionAsync(SubmitTriviaAnswerCommand request, ILiveSessionRepository repository, CancellationToken cancellationToken)
    {
        return await repository.GetBySessionTeamIdWithEvidenceSubmissionsAsync(request.SessionTeamId, cancellationToken);
    }

    protected override Exception CreateNotFoundException(SubmitTriviaAnswerCommand request)
    {
        return new UmbralDomainException(
            "session_team_not_found",
            $"Session Team '{request.SessionTeamId}' was not found.",
            UmbralFailureCategory.NotFound);
    }

    protected override Guid GetSessionTeamId(SubmitTriviaAnswerCommand request, LiveSession session)
        => request.SessionTeamId;

    protected override void EnsurePermissions(SubmitTriviaAnswerCommand request, LiveSession session)
    {
        var participantUserId = currentParticipantIdentity.GetRequiredParticipantUserId();
        EnsureParticipantBelongsToSessionTeam(session, request.SessionTeamId, participantUserId.Value);
    }

    protected override Task<DomainStepResult> ExecuteDomainStepAsync(SubmitTriviaAnswerCommand request, LiveSession session, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
    {
        var evidenceSubmission = session.SubmitTriviaAnswer(
            request.SessionTeamId,
            request.SelectedChoiceId,
            occurredAtUtc);

        var result = new DomainStepResult(
            EvidenceSubmission: evidenceSubmission,
            PublishOutcomeChanged: true,
            PreviousOutcome: null,
            OutcomeChangedReason: "Trivia Evidence Submission validation outcome changed.",
            RecordStageCredit: evidenceSubmission.Outcome == ValidationOutcome.Accepted,
            IsValidationOverride: false,
            PublishProgressChanged: evidenceSubmission.Outcome == ValidationOutcome.Accepted,
            ProgressChangedReason: "Trivia answer accepted; Session Team progression changed.",
            StateChangedReason: "LiveSession finalized after Trivia Evidence Submission."
        );

        return Task.FromResult(result);
    }

    protected override SubmitEvidenceResponse CreateResponse(SubmitTriviaAnswerCommand request, LiveSession session, EvidenceSubmission evidenceSubmission, DomainStepResult domainResult, LiveSessionStage? currentStage, string progressState, DateTimeOffset occurredAtUtc)
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
