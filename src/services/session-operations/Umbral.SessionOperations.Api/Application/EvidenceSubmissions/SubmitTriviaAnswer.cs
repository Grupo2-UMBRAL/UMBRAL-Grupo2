using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Application.SessionEnrollment;
using Umbral.SessionOperations.Api.Application.SessionSnapshots;
using Umbral.SessionOperations.Api.Domain.LiveSessions;
using Umbral.SessionOperations.Api.Hubs;
using Umbral.SessionOperations.Api.Hubs.Contracts;
using Umbral.SessionOperations.Api.Infrastructure;

namespace Umbral.SessionOperations.Api.Application.EvidenceSubmissions;

public sealed record SubmitTriviaAnswerCommand(Guid SessionTeamId, string AnswerText) : IRequest<SubmitEvidenceResponse>;

public sealed record SubmitTriviaAnswerRequest(string AnswerText);

public sealed class SubmitTriviaAnswerHandler(
    SessionOperationsDbContext dbContext,
    TimeProvider timeProvider,
    ICurrentParticipantIdentity currentParticipantIdentity,
    IHubContext<SessionOperationsHub, ISessionClient> hubContext)
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
        var evidenceSubmission = liveSession.SubmitTriviaAnswer(
            request.SessionTeamId,
            request.AnswerText,
            submittedAtUtc);
        var currentStage = liveSession.GetCurrentStageForTeam(request.SessionTeamId);
        var progressState = liveSession.GetProgressStateForTeam(request.SessionTeamId);

        await dbContext.SaveChangesAsync(cancellationToken);

        await PublishEvidenceSubmissionOutcomeChangedAsync(
            liveSession,
            evidenceSubmission,
            previousOutcome: null,
            submittedAtUtc,
            cancellationToken);

        if (evidenceSubmission.Outcome == ValidationOutcome.Accepted)
        {
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

        await hubContext.Clients.All.ReceiveEvidenceSubmissionOutcomeChanged(payload).WaitAsync(cancellationToken);
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

        await hubContext.Clients.All.ReceiveTeamProgressChanged(payload).WaitAsync(cancellationToken);
    }

    private async Task PublishSessionStateChangedAsync(
        LiveSession liveSession,
        string previousState,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var payload = new SessionStateChangedPayload(
            CreateMetadata(liveSession, occurredAtUtc, "LiveSession finalized after Trivia Evidence Submission."),
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
            currentStage.GameType);
    }
}
