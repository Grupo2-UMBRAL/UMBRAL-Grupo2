using SessionManagement.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Scoring;
using SessionManagement.Application.Realtime;
using SessionManagement.Application.Features.SessionSnapshots;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Application.Hubs.Contracts;
using SessionManagement.Application.Features.SessionLifecycle;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.EvidenceSubmissions;

public sealed class OverrideValidationOutcomeHandler(
    IUnitOfWork unitOfWork, IRepository<LiveSession> liveSessionRepository,
    TimeProvider timeProvider,
    ICurrentOperatorIdentity currentOperatorIdentity,
    ISessionRealtimeNotifier realtimeNotifier,
    IScoringMonitoringClient scoringAuditClient)
    : IRequestHandler<OverrideValidationOutcomeCommand, OverrideValidationOutcomeResponse>
{
    public async Task<OverrideValidationOutcomeResponse> Handle(
        OverrideValidationOutcomeCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await liveSessionRepository
            .Include(session => session.SessionTeams)
            .Include(session => session.TeamProgressions)
            .Include(session => session.EvidenceSubmissions)
            .Include(session => session.ValidationOverrideLogs)
            .SingleOrDefaultAsync(
                session => session.EvidenceSubmissions.Any(submission => submission.Id == request.EvidenceSubmissionId),
                cancellationToken);
        if (liveSession is null)
        {
            throw new UmbralDomainException(
                "evidence_submission_not_found",
                $"Evidence Submission '{request.EvidenceSubmissionId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        var evidenceSubmission = liveSession.EvidenceSubmissions.Single(submission => submission.Id == request.EvidenceSubmissionId);
        var previousOutcome = evidenceSubmission.Outcome.ToString();
        var previousProgressState = liveSession.GetProgressStateForTeam(evidenceSubmission.SessionTeamId);
        var previousStage = liveSession.GetCurrentStageForTeam(evidenceSubmission.SessionTeamId);
        var stageStartedAtUtc = GetCurrentStageStartedAtUtc(liveSession, evidenceSubmission.SessionTeamId);
        var previousLiveSessionState = liveSession.State;
        var overriddenAtUtc = timeProvider.GetUtcNow();
        var operatorUserId = currentOperatorIdentity.GetRequiredOperatorUserId();
        var validationOverrideLog = liveSession.OverrideValidationOutcome(
            request.EvidenceSubmissionId,
            operatorUserId,
            request.IsAccepted,
            request.Reason,
            overriddenAtUtc);
        var currentStage = liveSession.GetCurrentStageForTeam(evidenceSubmission.SessionTeamId);
        var progressState = liveSession.GetProgressStateForTeam(evidenceSubmission.SessionTeamId);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await scoringAuditClient.LogSessionEventAsync(
            liveSession.Id,
            "ValidationOutcome",
            $"Evidence submission '{evidenceSubmission.Id}' for Session Team '{evidenceSubmission.SessionTeamId}' on Mission Stage '{evidenceSubmission.MissionStageId}' was validated as {evidenceSubmission.Outcome}. Source: OperatorOverride. Reason: {request.Reason}.",
            cancellationToken);

        if (!string.Equals(previousOutcome, ValidationOutcome.Accepted.ToString(), StringComparison.Ordinal)
            && evidenceSubmission.Outcome == ValidationOutcome.Accepted)
        {
            await scoringAuditClient.RecordStageCreditAsync(
                CreateStageCreditRequest(
                    liveSession,
                    evidenceSubmission,
                    stageStartedAtUtc,
                    overriddenAtUtc),
                cancellationToken);
        }

        await PublishEvidenceSubmissionOutcomeChangedAsync(
            liveSession,
            evidenceSubmission,
            previousOutcome,
            overriddenAtUtc,
            cancellationToken);

        if (!string.Equals(previousProgressState, progressState, StringComparison.Ordinal)
            || previousStage?.MissionStageId != currentStage?.MissionStageId)
        {
            await PublishTeamProgressChangedAsync(
                liveSession,
                evidenceSubmission.SessionTeamId,
                previousStage,
                currentStage,
                progressState,
                overriddenAtUtc,
                cancellationToken);
        }

        if (!string.Equals(previousLiveSessionState, liveSession.State, StringComparison.Ordinal))
        {
            await PublishSessionStateChangedAsync(
                liveSession,
                previousLiveSessionState,
                overriddenAtUtc,
                cancellationToken);
        }

        return new OverrideValidationOutcomeResponse(
            liveSession.Id,
            evidenceSubmission.Id,
            validationOverrideLog.Id,
            evidenceSubmission.SessionTeamId,
            evidenceSubmission.MissionStageId,
            previousOutcome,
            evidenceSubmission.Outcome.ToString(),
            progressState,
            MapCurrentStage(currentStage),
            liveSession.SequenceNumber,
            overriddenAtUtc);
    }

    private async Task PublishEvidenceSubmissionOutcomeChangedAsync(
        LiveSession liveSession,
        EvidenceSubmission evidenceSubmission,
        string previousOutcome,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var payload = new EvidenceSubmissionOutcomeChangedPayload(
            CreateMetadata(liveSession, occurredAtUtc, "Validation Override changed Evidence Submission outcome."),
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
        CancellationToken cancellationToken)
    {
        var payload = new TeamProgressChangedPayload(
            CreateMetadata(liveSession, occurredAtUtc, "Validation Override accepted Evidence Submission; Session Team progression changed."),
            sessionTeamId,
            MapCurrentStage(previousStage),
            MapCurrentStage(currentStage),
            progressState);

        await realtimeNotifier.NotifyTeamProgressChangedAsync(payload, cancellationToken);
    }

    private async Task PublishSessionStateChangedAsync(
        LiveSession liveSession,
        string previousState,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
        => await realtimeNotifier.NotifySessionStateChangedAsync(
            new LiveSessionStateChangedEvent(
                liveSession.Id,
                previousState,
                liveSession.State,
                liveSession.SessionTeams.Count,
                liveSession.SequenceNumber,
                "LiveSession finalized after Validation Override.",
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

    private static CurrentSessionStageSnapshot? MapCurrentStage(LiveSessionStage? currentStage)
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
            currentStage.Prompt);
    }

    private static RecordStageCreditRequest CreateStageCreditRequest(
        LiveSession liveSession,
        EvidenceSubmission evidenceSubmission,
        DateTimeOffset stageStartedAtUtc,
        DateTimeOffset recordedAtUtc)
    {
        var acceptedStage = liveSession.SessionStageFlow
            .FirstOrDefault(stage => stage.MissionStageId == evidenceSubmission.MissionStageId);
        if (acceptedStage is null)
        {
            throw new UmbralDomainException(
                "score_stage_credit_stage_required",
                "Accepted Validation Override must reference a Session Stage to record Stage Credit.",
                UmbralFailureCategory.Conflict);
        }

        return new RecordStageCreditRequest(
            liveSession.Id,
            evidenceSubmission.SessionTeamId,
            acceptedStage.MissionStageId,
            acceptedStage.Difficulty,
            CalculateResolutionTime(stageStartedAtUtc, evidenceSubmission.SubmittedAtUtc),
            recordedAtUtc,
            true);
    }

    private static DateTimeOffset GetCurrentStageStartedAtUtc(LiveSession liveSession, Guid sessionTeamId)
        => liveSession.TeamProgressions
            .FirstOrDefault(progress => progress.SessionTeamId == sessionTeamId)
            ?.UpdatedAtUtc
            ?? liveSession.ScheduledStartAtUtc
            ?? liveSession.CreatedAtUtc;

    private static TimeSpan CalculateResolutionTime(DateTimeOffset stageStartedAtUtc, DateTimeOffset submittedAtUtc)
        => submittedAtUtc >= stageStartedAtUtc
            ? submittedAtUtc - stageStartedAtUtc
            : TimeSpan.Zero;
}



