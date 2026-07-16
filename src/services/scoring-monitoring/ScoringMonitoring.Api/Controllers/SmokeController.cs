using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbral.ServiceDefaults;

namespace ScoringMonitoring.Api.Controllers;

[ApiController]
[Route("api/scoring-monitoring/smoke")]
[Tags("Authorization smoke")]
public sealed class SmokeController : ControllerBase
{
    [HttpGet("administrator")]
    [Authorize(Roles = UmbralRoles.Administrator)]
    [EndpointSummary("Verify administrator authorization")]
    [EndpointDescription("Confirms that the request is authorized for the Administrator role.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public IActionResult SmokeAdministrator() => Ok();

    [HttpGet("operator")]
    [Authorize(Roles = UmbralRoles.Operator)]
    [EndpointSummary("Verify operator authorization")]
    [EndpointDescription("Confirms that the request is authorized for the Operator role.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public IActionResult SmokeOperator() => Ok();

    [HttpGet("participant")]
    [Authorize(Roles = UmbralRoles.Participant)]
    [EndpointSummary("Verify participant authorization")]
    [EndpointDescription("Confirms that the request is authorized for the Participant role.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public IActionResult SmokeParticipant() => Ok();
}
