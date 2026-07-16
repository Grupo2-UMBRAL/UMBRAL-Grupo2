using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScoringMonitoring.Application.Features.Rankings;
using ScoringMonitoring.Application.Features.Rankings.Queries.GetRanking;
using ScoringMonitoring.Application.Features.Scoreboards.Commands.ApplyPenalty;
using ScoringMonitoring.Application.Features.Scoreboards.Commands.RecordStageCredit;
using ScoringMonitoring.Application.Features.SessionEventLogs;
using ScoringMonitoring.Application.Features.SessionEventLogs.Commands.LogSessionEvent;
using ScoringMonitoring.Application.Features.SessionEventLogs.Queries.GetSessionEventLog;
using Umbral.ServiceDefaults;

namespace ScoringMonitoring.Api.Controllers;

[ApiController]
[Route("api/scoring-monitoring/sessions")]
[Authorize(Roles = $"{UmbralRoles.Administrator},{UmbralRoles.Operator},{UmbralRoles.Participant}")]
[Tags("Scoreboard and ranking")]
public sealed class ScoringMonitoringController(ISender sender) : ControllerBase
{
    [HttpPost("{liveSessionId:guid}/scores")]
    [EndpointSummary("Record Play credit")]
    [EndpointDescription("Records a Score Entry for a resolved Play and returns the updated Ranking.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RecordStageCreditResponse>> RecordStageCredit(
        Guid liveSessionId,
        [FromBody] RecordStageCreditRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await sender.Send(
            new RecordStageCreditCommand(
                liveSessionId,
                request.SessionTeamId,
                request.PlayId,
                request.Difficulty,
                request.ResolutionTime,
                request.RecordedAt,
                request.ValidationOverride),
            cancellationToken);

        return Ok(response);
    }

    [HttpPost("{liveSessionId:guid}/penalties")]
    [Authorize(Roles = $"{UmbralRoles.Administrator},{UmbralRoles.Operator}")]
    [EndpointSummary("Apply a penalty")]
    [EndpointDescription("Applies an explicit Penalty to a Scoreboard and returns the updated Ranking.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplyPenaltyResponse>> ApplyPenalty(
        Guid liveSessionId,
        [FromBody] ApplyPenaltyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await sender.Send(
            new ApplyPenaltyCommand(
                liveSessionId,
                request.SessionTeamId,
                request.CommandId,
                request.Severity,
                request.AppliedByOperatorUserId,
                request.Reason,
                request.RecordedAt),
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("{liveSessionId:guid}/ranking")]
    [EndpointSummary("Get ranking")]
    [EndpointDescription("Returns the Ranking derived from the LiveSession Scoreboard.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RankingPayload>> GetRanking(
        Guid liveSessionId,
        CancellationToken cancellationToken)
    {
        var ranking = await sender.Send(new GetRankingQuery(liveSessionId), cancellationToken);
        return Ok(ranking);
    }

    [HttpGet("{liveSessionId:guid}/event-log")]
    [Authorize(Roles = $"{UmbralRoles.Administrator},{UmbralRoles.Operator}")]
    [EndpointSummary("Get Session Event Log")]
    [EndpointDescription("Returns the auditable Session Event Log for a LiveSession.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<SessionEventLogPayload>>> GetSessionEventLog(
        Guid liveSessionId,
        CancellationToken cancellationToken)
    {
        var eventLog = await sender.Send(new GetSessionEventLogQuery(liveSessionId), cancellationToken);
        return Ok(eventLog);
    }

    [HttpPost("{liveSessionId:guid}/event-log")]
    [EndpointSummary("Record a Session Event Log entry")]
    [EndpointDescription("Records an auditable event associated with a LiveSession.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SessionEventLogPayload>> LogSessionEvent(
        Guid liveSessionId,
        [FromBody] LogSessionEventRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = await sender.Send(
            new LogSessionEventCommand(liveSessionId, request.EventType, request.Description),
            cancellationToken);

        return Ok(payload);
    }
}
