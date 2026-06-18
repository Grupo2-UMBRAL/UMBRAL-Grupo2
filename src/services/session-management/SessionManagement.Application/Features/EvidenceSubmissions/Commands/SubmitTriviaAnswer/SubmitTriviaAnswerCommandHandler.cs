using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Scoring;
using SessionManagement.Application.Realtime;
using SessionManagement.Application.Features.SessionEnrollment;
using SessionManagement.Application.Features.SessionSnapshots;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Application.Hubs.Contracts;
using SessionManagement.Application.Features.SessionLifecycle;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.EvidenceSubmissions;

public sealed class SubmitTriviaAnswerHandler(
    ISessionManagementDbContext dbContext,
    TimeProvider timeProvider,
    ICurrentParticipantIdentity currentParticipantIdentity,
    ISessionRealtimeNotifier realtimeNotifier,
    IScoringMonitoringClient scoringAuditClient)
    : IRequestHandler<SubmitTriviaAnswerCommand, SubmitEvidenceResponse>
{
    public async Task<SubmitEvidenceResponse> Handle(
        SubmitTriviaAnswerCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var participantUserId = currentParticipantIdentity.GetRequiredParticipantUserId();
        var liveSession = await dbContext.LiveSessions
            .Include(session => session.SessionTeams)
            .Include(session => session.TeamParticipations)
            .Include(session => session.TeamProgressions)
            .Include(session => session.EvidenceSubmissions)
            .SingleOrDefaultAsync(
                session => session.SessionTeams.Any(team => team.Id == request.SessionTeamId),
                cancellationToken);
        if (liveSession is null)
        {
            throw new UmbralDomainException(
                "session_team_not_found",
                $"Session Team '{request.SessionTeamId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        EnsureParticipantBelongsToSessionTeam(liveSession, request.SessionTeamId, participantUserId.Value);

        var submittedAtUtc = timeProvider.GetUtcNow();
        var previousLiveSessionState = liveSession.State;
        var previousStage = liveSession.GetCurrentStageForTeam(request.SessionTeamId);
        var stageStartedAtUtc = GetCurrentStageStartedAtUtc(liveSession, request.SessionTeamId);
        var evidenceSubmission = liveSession.SubmitTriviaAnswer(
            request.SessionTeamId,
            request.AnswerText,
            submittedAtUtc);
        var currentStage = liveSession.GetCurrentStageForTeam(request.SessionTeamId);
        var progressState = liveSession.GetProgressStateForTeam(request.SessionTeamId);

        await dbContext.SaveChangesAsync(cancellationToken);

        await LogEvidenceSubmissionEventsAsync(
            evidenceSubmission,
            "AutomaticTrivia",
            cancellationToken);

        await PublishEvidenceSubmissionOutcomeChangedAsync(
            liveSession,
            evidenceSubmission,
            previousOutcome: null,
            submittedAtUtc,
            cancellationToken);

        if (evidenceSubmission.Outcome == ValidationOutcome.Accepted)
        {
            await scoringAuditClient.RecordStageCreditAsync(
                CreateStageCreditRequest(
                    liveSession.Id,
                    request.SessionTeamId,
                    previousStage,
                    stageStartedAtUtc,
                    submittedAtUtc,
                    validationOverride: false),
                cancellationToken);

            await PublishTeamProgressChangedAsync(
                liveSession,
                request.SessionTeamId,
                previousStage,
                currentStage,
                progressState,
                submittedAtUtc,
                "Trivia answer accepted; Session Team progression changed.",
                cancellationToken);

            if (!string.Equals(previousLiveSessionState, liveSession.State, StringComparison.Ordinal))
            {
                await PublishSessionStateChangedAsync(
                    liveSession,
                    previousLiveSessionState,
                    submittedAtUtc,
                    cancellationToken);
            }
        }

        return new SubmitEvidenceResponse(
            liveSession.Id,
            request.SessionTeamId,
            evidenceSubmission.Id,
            evidenceSubmission.Outcome.ToString(),
            progressState,
            MapCurrentStage(currentStage),
            liveSession.SequenceNumber,
            submittedAtUtc);
    }

    private static void EnsureParticipantBelongsToSessionTeam(
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
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var payload = new EvidenceSubmissionOutcomeChangedPayload(
            CreateMetadata(liveSession, occurredAtUtc, "Trivia Evidence Submission validation outcome changed."),
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
                "LiveSession finalized after Trivia Evidence Submission.",
                occurredAtUtc),
            cancellationToken);

    private async Task LogEvidenceSubmissionEventsAsync(
        EvidenceSubmission evidenceSubmission,
        string source,
        CancellationToken cancellationToken)
    {
        await scoringAuditClient.LogSessionEventAsync(
            evidenceSubmission.LiveSessionId,
            "EvidenceSubmitted",
            $"Session Team '{evidenceSubmission.SessionTeamId}' submitted evidence for Mission Stage '{evidenceSubmission.MissionStageId}'. Game Type: {evidenceSubmission.GameType}.",
            cancellationToken);

        await scoringAuditClient.LogSessionEventAsync(
            evidenceSubmission.LiveSessionId,
            "ValidationOutcome",
            $"Evidence submission '{evidenceSubmission.Id}' for Session Team '{evidenceSubmission.SessionTeamId}' on Mission Stage '{evidenceSubmission.MissionStageId}' was validated as {evidenceSubmission.Outcome}. Source: {source}.",
            cancellationToken);
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
        Guid liveSessionId,
        Guid sessionTeamId,
        LiveSessionStage? acceptedStage,
        DateTimeOffset stageStartedAtUtc,
        DateTimeOffset recordedAtUtc,
        bool validationOverride)
    {
        if (acceptedStage is null)
        {
            throw new UmbralDomainException(
                "score_stage_credit_stage_required",
                "Accepted Evidence Submission must have a Session Stage to record Stage Credit.",
                UmbralFailureCategory.Conflict);
        }

        return new RecordStageCreditRequest(
            liveSessionId,
            sessionTeamId,
            acceptedStage.MissionStageId,
            acceptedStage.Difficulty,
            CalculateResolutionTime(stageStartedAtUtc, recordedAtUtc),
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
