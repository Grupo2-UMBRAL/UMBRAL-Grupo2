using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MissionManagement.Application.Features.Missions.Queries.GetEligibleMissionForLiveSession;
using MissionManagement.Application.Features.Missions.Queries.ListEligibleMissionsForLiveSession;
using Umbral.ServiceDefaults;

namespace MissionManagement.Api.Controllers;

[ApiController]
[Route("api/mission-management/missions/eligible-for-live-session")]
[Authorize(Roles = $"{UmbralRoles.Administrator},{UmbralRoles.Operator}")]
[Tags("Mission eligibility")]
public sealed class EligibleMissionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List missions eligible for a LiveSession")]
    [EndpointDescription("Returns active Mission templates that can be used to create a LiveSession.")]
    [ProducesResponseType(typeof(IReadOnlyList<EligibleMissionForLiveSessionSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<EligibleMissionForLiveSessionSummaryResponse>>> List(
        CancellationToken cancellationToken)
    {
        var missions = await sender.Send(new ListEligibleMissionsForLiveSessionQuery(), cancellationToken);
        return Ok(missions);
    }

    [HttpGet("{missionId:guid}")]
    [EndpointSummary("Get an eligible mission for a LiveSession")]
    [EndpointDescription("Returns the flattened playable definition used to create a LiveSession from a Mission.")]
    [ProducesResponseType(typeof(EligibleMissionForLiveSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EligibleMissionForLiveSessionResponse>> GetById(
        Guid missionId,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(new GetEligibleMissionForLiveSessionQuery(missionId), cancellationToken);
        return Ok(mission);
    }
}
