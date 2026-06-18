using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SessionManagement.Application.Features.SessionEnrollment;
using Umbral.ServiceDefaults;

namespace SessionManagement.Api.Controllers;

[ApiController]
[Route("api/session-management")]
[Authorize]
public sealed class SessionEnrollmentController(ISender sender) : ControllerBase
{
    [HttpPost("live-sessions/{liveSessionId:guid}/session-enrollment/join-code")]
    [Authorize(Roles = $"{UmbralRoles.Administrator},{UmbralRoles.Operator}")]
    public async Task<ActionResult<GenerateJoinCodeResponse>> GenerateJoinCode(
        Guid liveSessionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GenerateJoinCodeCommand(liveSessionId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("live-sessions/{liveSessionId:guid}/session-enrollment/window/open")]
    [Authorize(Roles = $"{UmbralRoles.Administrator},{UmbralRoles.Operator}")]
    public async Task<ActionResult<EnrollmentWindowResponse>> OpenEnrollmentWindow(
        Guid liveSessionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new OpenEnrollmentWindowCommand(liveSessionId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("live-sessions/{liveSessionId:guid}/session-enrollment/window/close")]
    [Authorize(Roles = $"{UmbralRoles.Administrator},{UmbralRoles.Operator}")]
    public async Task<ActionResult<EnrollmentWindowResponse>> CloseEnrollmentWindow(
        Guid liveSessionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CloseEnrollmentWindowCommand(liveSessionId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("session-enrollment/{joinCode}/validate")]
    [Authorize(Roles = UmbralRoles.Participant)]
    public async Task<ActionResult<ParticipantEnrollmentStatusResponse>> ValidateJoinCode(
        string joinCode,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ValidateJoinCodeQuery(joinCode), cancellationToken);
        return Ok(result);
    }

    [HttpGet("session-enrollment/{joinCode}/teams")]
    [Authorize(Roles = UmbralRoles.Participant)]
    public async Task<ActionResult<SessionTeamsResponse>> ListSessionTeams(
        string joinCode,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListSessionTeamsQuery(joinCode), cancellationToken);
        return Ok(result);
    }

    [HttpPost("session-enrollment/teams")]
    [Authorize(Roles = UmbralRoles.Participant)]
    public async Task<ActionResult<RegisterTeamResponse>> RegisterTeam(
        [FromBody] RegisterTeamRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RegisterTeamCommand(request.JoinCode, request.TeamName),
            cancellationToken);
        return Created(
            $"api/session-management/live-sessions/{result.LiveSessionId}/session-teams/{result.SessionTeamId}",
            result);
    }

    [HttpPost("session-enrollment/join")]
    [Authorize(Roles = UmbralRoles.Participant)]
    public async Task<ActionResult<JoinSessionTeamResponse>> JoinSessionTeam(
        [FromBody] JoinSessionTeamRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new JoinSessionTeamCommand(request.JoinCode, request.SessionTeamId),
            cancellationToken);
        return Ok(result);
    }
}
