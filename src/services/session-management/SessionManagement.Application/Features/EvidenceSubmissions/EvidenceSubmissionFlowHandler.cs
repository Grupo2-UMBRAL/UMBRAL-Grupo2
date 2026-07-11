using MediatR;
using SessionManagement.Application.Abstractions;
using SessionManagement.Application.Abstractions.Realtime;
using SessionManagement.Application.Abstractions.Scoring;
using SessionManagement.Application.Features.SessionLifecycle;
using SessionManagement.Application.Features.SessionSnapshots;
using SessionManagement.Domain.LiveSessions;
using Umbral.ServiceDefaults;

namespace SessionManagement.Application.Features.EvidenceSubmissions;

public abstract class EvidenceSubmissionFlowHandler<TCommand, TResponse>(
    IUnitOfWork unitOfWork,
    ILiveSessionRepository liveSessionRepository,
    TimeProvider timeProvider,
    ISessionRealtimeNotifier realtimeNotifier,
    IScoringMonitoringClient scoringAuditClient)
    : IRequestHandler<TCommand, TResponse>
    where TCommand : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await GetLiveSessionAsync(request, liveSessionRepository, cancellationToken);
        if (liveSession is null)
        {
            throw CreateNotFoundException(request);
        }

        EnsurePermissions(request, liveSession);

        var sessionTeamId = GetSessionTeamId(request, liveSession);
        
        var occurredAtUtc = timeProvider.GetUtcNow();
        var previousLiveSessionState = liveSession.State;
        var previousStage = liveSession.GetCurrentStageForTeam(sessionTeamId);
        var stageStartedAtUtc = GetCurrentStageStartedAtUtc(liveSession, sessionTeamId);

        var domainResult = await ExecuteDomainStepAsync(request, liveSession, occurredAtUtc, cancellationToken);
        var evidenceSubmission = domainResult.EvidenceSubmission;

        var currentStage = liveSession.GetCurrentStageForTeam(sessionTeamId);
        var progressState = liveSession.GetProgressStateForTeam(sessionTeamId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Preserve the per-command ordering of the original handlers: the participant submit
        // paths broadcast the outcome before recording the Stage Credit, whereas a Validation
        // Override records the credit first. Under a scoring failure the two orders differ in
        // whether the realtime outcome event has already been emitted.
        async Task PublishOutcomeChangedIfNeededAsync()
        {
            if (domainResult.PublishOutcomeChanged)
            {
                await PublishEvidenceSubmissionOutcomeChangedAsync(
                    liveSession,
                    evidenceSubmission,
                    domainResult.PreviousOutcome,
                    domainResult.OutcomeChangedReason!,
                    occurredAtUtc,
                    cancellationToken);
            }
        }

        async Task RecordStageCreditIfNeededAsync()
        {
            if (domainResult.RecordStageCredit)
            {
                await scoringAuditClient.RecordStageCreditAsync(
                    CreateStageCreditRequest(
                        liveSession,
                        evidenceSubmission,
                        stageStartedAtUtc,
                        domainResult.StageCreditRecordedAtUtc ?? occurredAtUtc,
                        domainResult.IsValidationOverride),
                    cancellationToken);
            }
        }

        if (domainResult.RecordStageCreditBeforeOutcomeChanged)
        {
            await RecordStageCreditIfNeededAsync();
            await PublishOutcomeChangedIfNeededAsync();
        }
        else
        {
            await PublishOutcomeChangedIfNeededAsync();
            await RecordStageCreditIfNeededAsync();
        }

        if (domainResult.PublishProgressChanged)
        {
            await PublishTeamProgressChangedAsync(
                liveSession,
                sessionTeamId,
                previousStage,
                currentStage,
                progressState,
                occurredAtUtc,
                domainResult.ProgressChangedReason!,
                cancellationToken);
        }

        if (previousLiveSessionState != liveSession.State)
        {
            await PublishSessionStateChangedAsync(
                liveSession,
                previousLiveSessionState,
                domainResult.StateChangedReason,
                occurredAtUtc,
                cancellationToken);
        }

        return CreateResponse(
            request,
            liveSession,
            evidenceSubmission,
            domainResult,
            currentStage,
            progressState,
            occurredAtUtc);
    }

    protected abstract Task<LiveSession?> GetLiveSessionAsync(TCommand request, ILiveSessionRepository repository, CancellationToken cancellationToken);
    protected abstract Exception CreateNotFoundException(TCommand request);
    protected abstract Guid GetSessionTeamId(TCommand request, LiveSession session);
    protected abstract void EnsurePermissions(TCommand request, LiveSession session);
    protected abstract Task<DomainStepResult> ExecuteDomainStepAsync(TCommand request, LiveSession session, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken);
    protected abstract TResponse CreateResponse(TCommand request, LiveSession session, EvidenceSubmission evidenceSubmission, DomainStepResult domainResult, LiveSessionStage? currentStage, string progressState, DateTimeOffset occurredAtUtc);

    protected static void EnsureParticipantBelongsToSessionTeam(
        LiveSession liveSession,
        Guid sessionTeamId,
        string participantUserId)
    {
        var participation = liveSession.TeamParticipations.FirstOrDefault(existingParticipation =>
            existingParticipation.SessionTeamId == sessionTeamId
            && string.Equals(existingParticipation.ParticipantUserId, participantUserId, StringComparison.Ordinal));
        if (participation is not null)
        {
            return;
        }

        throw new UmbralDomainException(
            "session_team_participation_required",
            "Participant must belong to this Session Team to submit evidence.",
            UmbralFailureCategory.Forbidden);
    }

    private async Task PublishEvidenceSubmissionOutcomeChangedAsync(
        LiveSession liveSession,
        EvidenceSubmission evidenceSubmission,
        string? previousOutcome,
        string reason,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var payload = new EvidenceSubmissionOutcomeChangedPayload(
            CreateMetadata(liveSession, occurredAtUtc, reason),
            evidenceSubmission.Id,
            evidenceSubmission.SessionTeamId,
            evidenceSubmission.MissionStageId,
            evidenceSubmission.Outcome.ToString(),
            previousOutcome,
            evidenceSubmission.FailureReason);

        await realtimeNotifier.NotifyEvidenceSubmissionOutcomeChangedAsync(payload, cancellationToken);
    }

    private async Task PublishTeamProgressChangedAsync(
        LiveSession liveSession,
        Guid sessionTeamId,
        LiveSessionStage? previousStage,
        LiveSessionStage? currentStage,
        string progressState,
        DateTimeOffset occurredAtUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        var payload = new TeamProgressChangedPayload(
            CreateMetadata(liveSession, occurredAtUtc, reason),
            sessionTeamId,
            MapCurrentStage(previousStage),
            MapCurrentStage(currentStage),
            progressState);

        await realtimeNotifier.NotifyTeamProgressChangedAsync(payload, cancellationToken);
    }

    private async Task PublishSessionStateChangedAsync(
        LiveSession liveSession,
        LiveSessionState previousState,
        string reason,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
        => await realtimeNotifier.NotifySessionStateChangedAsync(
            new LiveSessionStateChangedEvent(
                liveSession.Id,
                previousState.Value,
                liveSession.State.Value,
                liveSession.SessionTeams.Count,
                liveSession.SequenceNumber,
                reason,
                occurredAtUtc),
            cancellationToken);

    private static RealtimeEventMetadata CreateMetadata(
        LiveSession liveSession,
        DateTimeOffset occurredAtUtc,
        string reason)
        => new(
            liveSession.Id,
            liveSession.SequenceNumber,
            occurredAtUtc,
            SnapshotRefreshPolicy.RefreshSnapshot,
            reason);

    protected static CurrentSessionStageSnapshot? MapCurrentStage(LiveSessionStage? currentStage)
    {
        if (currentStage is null)
        {
            return null;
        }

        return new CurrentSessionStageSnapshot(
            currentStage.MissionStageId,
            currentStage.Name,
            currentStage.SessionStageOrder,
            currentStage.SourceOrder,
            currentStage.ResolvedTimeBudgetMinutes,
            currentStage.Difficulty,
            currentStage.GameType,
            currentStage.Prompt,
            currentStage.Choices.Select(choice => new SessionStageChoiceSnapshot(choice.Id, choice.Text)).ToArray());
    }

    private static RecordStageCreditRequest CreateStageCreditRequest(
        LiveSession liveSession,
        EvidenceSubmission evidenceSubmission,
        DateTimeOffset stageStartedAtUtc,
        DateTimeOffset recordedAtUtc,
        bool validationOverride)
    {
        var acceptedStage = liveSession.SessionStageFlow
            .FirstOrDefault(stage => stage.MissionStageId == evidenceSubmission.MissionStageId);
        
        if (acceptedStage is null)
        {
            throw new UmbralDomainException(
                "score_stage_credit_stage_required",
                "Accepted Evidence Submission must have a Session Stage to record Stage Credit.",
                UmbralFailureCategory.Conflict);
        }

        return new RecordStageCreditRequest(
            liveSession.Id,
            evidenceSubmission.SessionTeamId,
            acceptedStage.MissionStageId,
            acceptedStage.Difficulty,
            CalculateResolutionTime(stageStartedAtUtc, evidenceSubmission.SubmittedAtUtc),
            recordedAtUtc,
            validationOverride);
    }

    private static DateTimeOffset GetCurrentStageStartedAtUtc(LiveSession liveSession, Guid sessionTeamId)
        => liveSession.TeamProgressions
            .FirstOrDefault(progress => progress.SessionTeamId == sessionTeamId)
            ?.UpdatedAtUtc
            ?? liveSession.ScheduledStartAtUtc
            ?? liveSession.CreatedAtUtc;

    private static TimeSpan CalculateResolutionTime(DateTimeOffset stageStartedAtUtc, DateTimeOffset recordedAtUtc)
        => recordedAtUtc >= stageStartedAtUtc
            ? recordedAtUtc - stageStartedAtUtc
            : TimeSpan.Zero;
}

public sealed record DomainStepResult(
    EvidenceSubmission EvidenceSubmission,
    bool PublishOutcomeChanged,
    string? PreviousOutcome,
    string? OutcomeChangedReason,
    bool RecordStageCredit,
    bool IsValidationOverride,
    bool PublishProgressChanged,
    string? ProgressChangedReason,
    string StateChangedReason,
    Guid ValidationOverrideLogId = default,
    DateTimeOffset? StageCreditRecordedAtUtc = null,
    bool RecordStageCreditBeforeOutcomeChanged = false
);
