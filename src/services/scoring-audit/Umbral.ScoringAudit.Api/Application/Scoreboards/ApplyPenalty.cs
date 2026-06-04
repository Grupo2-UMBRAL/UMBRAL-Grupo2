using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Umbral.ScoringAudit.Api.Application.Rankings;
using Umbral.ScoringAudit.Api.Domain.Penalties;
using Umbral.ScoringAudit.Api.Domain.Scoreboards;
using Umbral.ScoringAudit.Api.Hubs;
using Umbral.ScoringAudit.Api.Hubs.Contracts;
using Umbral.ScoringAudit.Api.Infrastructure;
using Umbral.ServiceDefaults;

namespace Umbral.ScoringAudit.Api.Application.Scoreboards;

public sealed record ApplyPenaltyCommand(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid CommandId,
    string Severity,
    string AppliedByOperatorUserId,
    string Reason,
    DateTimeOffset RecordedAt) : IRequest<ApplyPenaltyResponse>;

public sealed record ApplyPenaltyRequest(
    Guid SessionTeamId,
    Guid CommandId,
    string Severity,
    string AppliedByOperatorUserId,
    string Reason,
    DateTimeOffset RecordedAt);

public sealed record ApplyPenaltyResponse(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid CommandId,
    Guid? PenaltyId,
    Guid? ScoreEntryId,
    bool PenaltyApplied,
    int VisibleScore,
    RankingPayload Ranking);

public sealed class ApplyPenaltyHandler(
    ScoringAuditDbContext dbContext,
    TimeProvider timeProvider,
    IHubContext<ScoringAuditHub, IScoringAuditClient> hubContext)
    : IRequestHandler<ApplyPenaltyCommand, ApplyPenaltyResponse>
{
    public async Task<ApplyPenaltyResponse> Handle(
        ApplyPenaltyCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scoreboard = await dbContext.Scoreboards
            .Include(entity => entity.ScoreEntries)
            .SingleOrDefaultAsync(entity => entity.LiveSessionId == request.LiveSessionId, cancellationToken);
        if (scoreboard is null)
        {
            scoreboard = new Scoreboard(request.LiveSessionId);
            dbContext.Scoreboards.Add(scoreboard);
        }
        else
        {
            scoreboard.RebuildState();
        }

        var penalty = new Penalty(
            Guid.NewGuid(),
            request.CommandId,
            request.SessionTeamId,
            ParseSeverity(request.Severity),
            request.AppliedByOperatorUserId,
            request.Reason,
            request.RecordedAt);

        ScoreEntry? scoreEntry = null;
        var penaltyApplied = false;

        try
        {
            scoreEntry = scoreboard.ApplyPenalty(penalty);
            await dbContext.SaveChangesAsync(cancellationToken);
            scoreboard.RebuildState();
            penaltyApplied = true;
        }
        catch (UmbralDomainException exception) when (exception.Code == "scoreboard.duplicate_penalty_command")
        {
            penaltyApplied = false;
        }

        var ranking = RankingProjection.Create(scoreboard, timeProvider.GetUtcNow());
        if (penaltyApplied)
        {
            await hubContext.Clients.All.ReceiveRankingUpdated(ranking).WaitAsync(cancellationToken);
        }

        return new ApplyPenaltyResponse(
            scoreboard.LiveSessionId,
            request.SessionTeamId,
            request.CommandId,
            penaltyApplied ? penalty.PenaltyId : null,
            scoreEntry?.ScoreEntryId,
            penaltyApplied,
            scoreboard.GetTeamScore(request.SessionTeamId).VisibleScore,
            ranking);
    }

    private static PenaltySeverity ParseSeverity(string severity)
    {
        if (Enum.TryParse<PenaltySeverity>(severity, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new UmbralDomainException(
            "penalty.unknown_severity",
            "Penalty Severity is not supported.",
            UmbralFailureCategory.Validation);
    }
}
