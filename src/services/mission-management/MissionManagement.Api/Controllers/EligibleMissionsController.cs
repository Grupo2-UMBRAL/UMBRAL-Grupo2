using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MissionManagement.Application.Features.Missions.Queries.GetEligibleMissionForLiveSession;
using MissionManagement.Application.Features.Missions.Queries.ListEligibleMissionsForLiveSession;
using Umbral.ServiceDefaults;

namespace MissionManagement.Api.Controllers;

[ApiController]
[Route("api/mission-management/missions/eligible-for-live-session")]
[Authorize(Roles = $"{UmbralRoles.Administrator},{UmbralRoles.Operator}")]
public sealed class EligibleMissionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EligibleMissionForLiveSessionSummaryResponse>>> List(
        CancellationToken cancellationToken)
    {
        var missions = await sender.Send(new ListEligibleMissionsForLiveSessionQuery(), cancellationToken);
        return Ok(missions);
    }

    [HttpGet("{missionId:guid}")]
    public async Task<ActionResult<EligibleMissionForLiveSessionResponse>> GetById(
        Guid missionId,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(new GetEligibleMissionForLiveSessionQuery(missionId), cancellationToken);
        return Ok(mission);
    }
}
