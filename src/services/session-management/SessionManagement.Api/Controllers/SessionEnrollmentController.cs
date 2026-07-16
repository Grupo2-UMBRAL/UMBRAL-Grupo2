using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SessionManagement.Application.Features.SessionEnrollment;
using Umbral.ServiceDefaults;

namespace SessionManagement.Api.Controllers;

[ApiController]
[Route("api/session-management")]
[Authorize]
[Tags("Session enrollment")]
public sealed class SessionEnrollmentController(ISender sender) : ControllerBase
{
    [HttpPost("live-sessions/{liveSessionId:guid}/session-enrollment/join-code")]
    [Authorize(Roles = $"{UmbralRoles.Administrator},{UmbralRoles.Operator}")]
    [EndpointSummary("Generate a Session Join Code")]
    [EndpointDescription("Generates the Session Join Code that participants use to enter a LiveSession.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GenerateJoinCodeResponse>> GenerateJoinCode(
        Guid liveSessionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GenerateJoinCodeCommand(liveSessionId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("live-sessions/{liveSessionId:guid}/session-enrollment/window/open")]
    [Authorize(Roles = $"{UmbralRoles.Administrator},{UmbralRoles.Operator}")]
    [EndpointSummary("Open the Team Assignment Window")]
    [EndpointDescription("Allows participants to create or join a Session Team in the LiveSession.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrollmentWindowResponse>> OpenEnrollmentWindow(
        Guid liveSessionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new OpenEnrollmentWindowCommand(liveSessionId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("live-sessions/{liveSessionId:guid}/session-enrollment/window/close")]
    [Authorize(Roles = $"{UmbralRoles.Administrator},{UmbralRoles.Operator}")]
    [EndpointSummary("Close the Team Assignment Window")]
    [EndpointDescription("Prevents further participant changes to Session Team assignments.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrollmentWindowResponse>> CloseEnrollmentWindow(
        Guid liveSessionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CloseEnrollmentWindowCommand(liveSessionId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("session-enrollment/{joinCode}/validate")]
    [Authorize(Roles = UmbralRoles.Participant)]
    [EndpointSummary("Validate a Session Join Code")]
    [EndpointDescription("Validates a Session Join Code for the authenticated participant.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ParticipantEnrollmentStatusResponse>> ValidateJoinCode(
        string joinCode,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ValidateJoinCodeQuery(joinCode), cancellationToken);
        return Ok(result);
    }

    [HttpGet("session-enrollment/{joinCode}/teams")]
    [Authorize(Roles = UmbralRoles.Participant)]
    [EndpointSummary("List session teams by join code")]
    [EndpointDescription("Lists the Session Teams a participant can join using a valid Session Join Code.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionTeamsResponse>> ListSessionTeams(
        string joinCode,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListSessionTeamsQuery(joinCode), cancellationToken);
        return Ok(result);
    }

    [HttpPost("session-enrollment/teams")]
    [Authorize(Roles = UmbralRoles.Participant)]
    [EndpointSummary("Create a session team")]
    [EndpointDescription("Creates a Session Team during the Team Assignment Window for the authenticated participant.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
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
    [EndpointSummary("Join a session team")]
    [EndpointDescription("Associates the authenticated participant with a Session Team during the Team Assignment Window.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
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
