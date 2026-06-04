using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Application.Scoring;
using Umbral.SessionOperations.Api.Application.SessionSnapshots;
using Umbral.SessionOperations.Api.Domain.LiveSessions;
using Umbral.SessionOperations.Api.Hubs;
using Umbral.SessionOperations.Api.Hubs.Contracts;
using Umbral.SessionOperations.Api.Infrastructure;

namespace Umbral.SessionOperations.Api.Application.EvidenceSubmissions;

public sealed record OverrideValidationOutcomeCommand(
    Guid EvidenceSubmissionId,
    bool IsAccepted,
    string Reason) : IRequest<OverrideValidationOutcomeResponse>;

public sealed record OverrideValidationOutcomeRequest(bool IsAccepted, string Reason);

public sealed record OverrideValidationOutcomeResponse(
    Guid LiveSessionId,
    Guid EvidenceSubmissionId,
    Guid ValidationOverrideLogId,
    Guid SessionTeamId,
    Guid MissionStageId,
    string PreviousOutcome,
    string NewOutcome,
    string ProgressState,
    CurrentSessionStageSnapshot? CurrentStage,
    long SequenceNumber,
    DateTimeOffset OverriddenAtUtc);

public sealed class OverrideValidationOutcomeHandler(
    SessionOperationsDbContext dbContext,
    TimeProvider timeProvider,
    ICurrentOperatorIdentity currentOperatorIdentity,
    IHubContext<SessionOperationsHub, ISessionClient> hubContext,
    IScoringAuditClient scoringAuditClient)
    : IRequestHandler<OverrideValidationOutcomeCommand, OverrideValidationOutcomeResponse>
{
    public async Task<OverrideValidationOutcomeResponse> Handle(
        OverrideValidationOutcomeCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await dbContext.LiveSessions
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

        await dbContext.SaveChangesAsync(cancellationToken);

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

        await hubContext.Clients.All.ReceiveEvidenceSubmissionOutcomeChanged(payload).WaitAsync(cancellationToken);
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

        await hubContext.Clients.All.ReceiveTeamProgressChanged(payload).WaitAsync(cancellationToken);
    }

    private async Task PublishSessionStateChangedAsync(
        LiveSession liveSession,
        string previousState,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var payload = new SessionStateChangedPayload(
            CreateMetadata(liveSession, occurredAtUtc, "LiveSession finalized after Validation Override."),
            previousState,
            liveSession.State,
            null);

        await hubContext.Clients.All.ReceiveSessionStateChanged(payload).WaitAsync(cancellationToken);
    }

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
