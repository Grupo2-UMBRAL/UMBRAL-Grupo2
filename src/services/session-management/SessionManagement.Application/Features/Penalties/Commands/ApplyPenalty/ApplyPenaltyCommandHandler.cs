using SessionManagement.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Features.EvidenceSubmissions;
using SessionManagement.Application.Scoring;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.Penalties;

public sealed class ApplyPenaltyHandler(
    IUnitOfWork unitOfWork, IRepository<LiveSession> liveSessionRepository,
    TimeProvider timeProvider,
    ICurrentOperatorIdentity currentOperatorIdentity,
    IScoringMonitoringClient scoringAuditClient)
    : IRequestHandler<ApplyPenaltyCommand, ApplyPenaltyResponse>
{
    public async Task<ApplyPenaltyResponse> Handle(
        ApplyPenaltyCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.CommandId == Guid.Empty)
        {
            throw new UmbralDomainException(
                "penalty_command_id_required",
                "Penalty command id is required.",
                UmbralFailureCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new UmbralDomainException(
                "penalty_reason_required",
                "Penalty reason is required.",
                UmbralFailureCategory.Validation);
        }

        var liveSession = await liveSessionRepository
            .Include(session => session.SessionTeams)
            .SingleOrDefaultAsync(session => session.Id == request.LiveSessionId, cancellationToken);
        if (liveSession is null)
        {
            throw new UmbralDomainException(
                "live_session_not_found",
                $"LiveSession '{request.LiveSessionId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        if (liveSession.SessionTeams.All(team => team.Id != request.SessionTeamId))
        {
            throw new UmbralDomainException(
                "session_team_not_found",
                $"Session Team '{request.SessionTeamId}' does not belong to this LiveSession.",
                UmbralFailureCategory.NotFound);
        }

        if (!string.Equals(liveSession.State, "Active", StringComparison.Ordinal)
            && !string.Equals(liveSession.State, "Paused", StringComparison.Ordinal))
        {
            throw new UmbralDomainException(
                "live_session_not_accepting_penalties",
                "LiveSession must be Active or Paused to apply a Penalty.",
                UmbralFailureCategory.Conflict);
        }

        var recordedAtUtc = timeProvider.GetUtcNow();
        var operatorUserId = currentOperatorIdentity.GetRequiredOperatorUserId();
        var scoringResponse = await scoringAuditClient.ApplyPenaltyAsync(
            new Scoring.ApplyPenaltyRequest(
                request.LiveSessionId,
                request.SessionTeamId,
                request.CommandId,
                request.Severity,
                operatorUserId,
                request.Reason,
                recordedAtUtc),
            cancellationToken);

        return new ApplyPenaltyResponse(
            scoringResponse.LiveSessionId,
            scoringResponse.SessionTeamId,
            scoringResponse.CommandId,
            scoringResponse.PenaltyId,
            scoringResponse.ScoreEntryId,
            scoringResponse.PenaltyApplied,
            scoringResponse.VisibleScore,
            scoringResponse.Ranking,
            recordedAtUtc);
    }
}



