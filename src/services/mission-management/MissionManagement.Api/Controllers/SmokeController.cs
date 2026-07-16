using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbral.ServiceDefaults;

namespace MissionManagement.Api.Controllers;

[ApiController]
[Route("api/mission-management/smoke")]
[Tags("Authorization smoke")]
public sealed class SmokeController : ControllerBase
{
    [HttpGet("administrator")]
    [Authorize(Roles = UmbralRoles.Administrator)]
    [EndpointSummary("Verify administrator authorization")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public IActionResult SmokeAdministrator() => Ok();

    [HttpGet("operator")]
    [Authorize(Roles = UmbralRoles.Operator)]
    [EndpointSummary("Verify operator authorization")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public IActionResult SmokeOperator() => Ok();

    [HttpGet("participant")]
    [Authorize(Roles = UmbralRoles.Participant)]
    [EndpointSummary("Verify participant authorization")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public IActionResult SmokeParticipant() => Ok();
}
