using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SessionManagement.Application.Features.Hints;
using SessionManagement.Application.Features.LiveSessions;
using SessionManagement.Application.Features.Penalties;
using SessionManagement.Application.Features.SessionLifecycle;
using SessionManagement.Application.Features.SessionSnapshots;
using Umbral.ServiceDefaults;

namespace SessionManagement.Api.Controllers;

[ApiController]
[Route("api/session-management/live-sessions")]
[Authorize(Roles = $"{UmbralRoles.Administrator},{UmbralRoles.Operator}")]
public sealed class LiveSessionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LiveSessionResponse>>> List(CancellationToken cancellationToken)
    {
        var sessions = await sender.Send(new ListLiveSessionsQuery(), cancellationToken);
        return Ok(sessions);
    }

    [HttpGet("{liveSessionId:guid}")]
    public async Task<ActionResult<LiveSessionResponse>> GetById(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var session = await sender.Send(new GetLiveSessionByIdQuery(liveSessionId), cancellationToken);
        return Ok(session);
    }

    [HttpGet("{liveSessionId:guid}/overview")]
    public async Task<ActionResult<LiveSessionOverview>> GetOverview(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var overview = await sender.Send(new GetLiveSessionOverviewQuery(liveSessionId), cancellationToken);
        return Ok(overview);
    }

    [HttpPost]
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
    public async Task<ActionResult<LiveSessionStateResponse>> Start(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new TransitionLiveSessionStateCommand(liveSessionId, LiveSessionLifecycleAction.Start),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{liveSessionId:guid}/lifecycle/pause")]
    public async Task<ActionResult<LiveSessionStateResponse>> Pause(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new TransitionLiveSessionStateCommand(liveSessionId, LiveSessionLifecycleAction.Pause),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{liveSessionId:guid}/lifecycle/resume")]
    public async Task<ActionResult<LiveSessionStateResponse>> Resume(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new TransitionLiveSessionStateCommand(liveSessionId, LiveSessionLifecycleAction.Resume),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{liveSessionId:guid}/lifecycle/finalize")]
    public async Task<ActionResult<LiveSessionStateResponse>> Finalize(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new TransitionLiveSessionStateCommand(liveSessionId, LiveSessionLifecycleAction.Finalize),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{liveSessionId:guid}/lifecycle/cancel")]
    public async Task<ActionResult<LiveSessionStateResponse>> Cancel(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new TransitionLiveSessionStateCommand(liveSessionId, LiveSessionLifecycleAction.Cancel),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{liveSessionId:guid}/penalties")]
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

    [HttpPost("{liveSessionId:guid}/hints/{hintId:guid}/release")]
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
