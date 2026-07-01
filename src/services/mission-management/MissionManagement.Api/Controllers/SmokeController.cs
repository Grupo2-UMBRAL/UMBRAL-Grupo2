using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbral.ServiceDefaults;

namespace MissionManagement.Api.Controllers;

[ApiController]
[Route("api/mission-management/smoke")]
public sealed class SmokeController : ControllerBase
{
    [HttpGet("administrator")]
    [Authorize(Roles = UmbralRoles.Administrator)]
    public IActionResult SmokeAdministrator() => Ok();

    [HttpGet("operator")]
    [Authorize(Roles = UmbralRoles.Operator)]
    public IActionResult SmokeOperator() => Ok();

    [HttpGet("participant")]
    [Authorize(Roles = UmbralRoles.Participant)]
    public IActionResult SmokeParticipant() => Ok();
}
