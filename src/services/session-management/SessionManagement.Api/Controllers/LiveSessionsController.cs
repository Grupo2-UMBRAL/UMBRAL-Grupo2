using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SessionManagement.Application.Features.Hints;
using SessionManagement.Application.Features.LiveSessions;
using SessionManagement.Application.Features.Penalties;
using SessionManagement.Application.Features.SessionEnrollment;
using SessionManagement.Application.Features.SessionLifecycle;
using SessionManagement.Application.Features.SessionSnapshots;
using Umbral.ServiceDefaults;

namespace SessionManagement.Api.Controllers;

[ApiController]
[Route("api/session-management/live-sessions")]
[Authorize(Roles = $"{UmbralRoles.Administrator},{UmbralRoles.Operator}")]
[Tags("Live sessions")]
public sealed class LiveSessionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List live sessions")]
    [EndpointDescription("Returns the LiveSessions available to administrators and operators.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<LiveSessionResponse>>> List(CancellationToken cancellationToken)
    {
        var sessions = await sender.Send(new ListLiveSessionsQuery(), cancellationToken);
        return Ok(sessions);
    }

    [HttpGet("{liveSessionId:guid}")]
    [EndpointSummary("Get a live session")]
    [EndpointDescription("Returns a LiveSession by its identifier.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LiveSessionResponse>> GetById(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var session = await sender.Send(new GetLiveSessionByIdQuery(liveSessionId), cancellationToken);
        return Ok(session);
    }

    [HttpGet("{liveSessionId:guid}/overview")]
    [EndpointSummary("Get live session overview")]
    [EndpointDescription("Returns the operational overview for a LiveSession.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LiveSessionOverview>> GetOverview(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var overview = await sender.Send(new GetLiveSessionOverviewQuery(liveSessionId), cancellationToken);
        return Ok(overview);
    }

    [HttpPost]
    [EndpointSummary("Create a live session")]
    [EndpointDescription("Creates a LiveSession with a snapshot of its selected Session Flow.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LiveSessionResponse>> Create(
        [FromBody] CreateLiveSessionRequest request,
        CancellationToken cancellationToken)
    {
        var liveSession = await sender.Send(
            new CreateLiveSessionCommand(
                request.MissionId,
                request.Name,
                request.ScheduledStartAtUtc,
                request.SelectedMissionStageIds),
            cancellationToken);

        return Created($"api/session-management/live-sessions/{liveSession.Id}", liveSession);
    }

    [HttpPost("{liveSessionId:guid}/lifecycle/start")]
    [EndpointSummary("Start a live session")]
    [EndpointDescription("Starts a LiveSession and closes its Team Assignment Window.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LiveSessionStateResponse>> Start(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new TransitionLiveSessionStateCommand(liveSessionId, LiveSessionLifecycleAction.Start),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{liveSessionId:guid}/lifecycle/pause")]
    [EndpointSummary("Pause a live session")]
    [EndpointDescription("Pauses an active LiveSession.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LiveSessionStateResponse>> Pause(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new TransitionLiveSessionStateCommand(liveSessionId, LiveSessionLifecycleAction.Pause),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{liveSessionId:guid}/lifecycle/resume")]
    [EndpointSummary("Resume a live session")]
    [EndpointDescription("Resumes a paused LiveSession.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LiveSessionStateResponse>> Resume(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new TransitionLiveSessionStateCommand(liveSessionId, LiveSessionLifecycleAction.Resume),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{liveSessionId:guid}/lifecycle/finalize")]
    [EndpointSummary("Finalize a live session")]
    [EndpointDescription("Finalizes a LiveSession after its operational work is complete.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LiveSessionStateResponse>> Finalize(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new TransitionLiveSessionStateCommand(liveSessionId, LiveSessionLifecycleAction.Finalize),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{liveSessionId:guid}/lifecycle/cancel")]
    [EndpointSummary("Cancel a live session")]
    [EndpointDescription("Cancels a LiveSession when its lifecycle permits the transition.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LiveSessionStateResponse>> Cancel(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new TransitionLiveSessionStateCommand(liveSessionId, LiveSessionLifecycleAction.Cancel),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{liveSessionId:guid}/penalties")]
    [EndpointSummary("Apply a penalty")]
    [EndpointDescription("Records a Penalty Application for a Session Team with a predefined severity and reason.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplyPenaltyResponse>> ApplyPenalty(
        Guid liveSessionId,
        [FromBody] ApplyPenaltyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ApplyPenaltyCommand(
                liveSessionId,
                request.SessionTeamId,
                request.CommandId,
                request.Severity,
                request.Reason),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{liveSessionId:guid}/session-teams")]
    [EndpointSummary("Register a session team as operator")]
    [EndpointDescription("Registers a Session Team in a LiveSession on behalf of an operator.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterTeamByOperatorResponse>> RegisterTeam(
        Guid liveSessionId,
        [FromBody] RegisterTeamByOperatorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RegisterTeamByOperatorCommand(liveSessionId, request.TeamName),
            cancellationToken);
        return Created($"api/session-management/live-sessions/{liveSessionId}/session-teams/{result.SessionTeamId}", result);
    }

    [HttpPost("{liveSessionId:guid}/hints/{hintId:guid}/release")]
    [EndpointSummary("Release a hint")]
    [EndpointDescription("Releases a Hint to a Session Team within a LiveSession.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyList<VisibleHintSnapshot>>> ReleaseHint(
        Guid liveSessionId,
        Guid hintId,
        [FromBody] ReleaseHintRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ReleaseHintCommand(liveSessionId, request.SessionTeamId, hintId),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{liveSessionId:guid}/stages/{missionStageId:guid}/hints")]
    [EndpointSummary("Create an operational hint")]
    [EndpointDescription("Creates a Hint for a pending Play in the Session Flow of a LiveSession.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LiveSessionStageHintResponse>> CreateOperationalHint(
        Guid liveSessionId,
        Guid missionStageId,
        [FromBody] CreateOperationalHintRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateOperationalHintCommand(
                liveSessionId,
                missionStageId,
                request.Content,
                request.Latitude,
                request.Longitude),
            cancellationToken);
        return Ok(result);
    }
}
