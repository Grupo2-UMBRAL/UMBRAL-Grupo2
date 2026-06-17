using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MissionDesign.Application.Features.MissionStages.Commands.CreateMissionStageHint;
using MissionDesign.Application.Features.MissionStages.Commands.DeactivateMissionStage;
using MissionDesign.Application.Features.MissionStages.Queries.GetMissionStageById;
using Umbral.ServiceDefaults;

namespace MissionDesign.Api.Controllers;

[ApiController]
[Route("api/mission-design/stages")]
[Authorize(Roles = UmbralRoles.Administrator)]
public sealed class MissionStagesController(ISender sender) : ControllerBase
{
    [HttpGet("{missionStageId:guid}")]
    public async Task<ActionResult<MissionStageResponse>> GetById(
        Guid missionStageId,
        CancellationToken cancellationToken)
    {
        var stage = await sender.Send(new GetMissionStageByIdQuery(missionStageId), cancellationToken);
        return Ok(stage);
    }

    [HttpPost("{missionStageId:guid}/hints")]
    public async Task<ActionResult<MissionStageHintResponse>> CreateHint(
        Guid missionStageId,
        [FromBody] CreateMissionStageHintRequest request,
        CancellationToken cancellationToken)
    {
        var hint = await sender.Send(
            new CreateMissionStageHintCommand(
                missionStageId,
                request.Content,
                request.IsSolution,
                request.Latitude,
                request.Longitude),
            cancellationToken);

        return Created($"api/mission-design/stages/{missionStageId}", hint);
    }

    [HttpPost("{missionStageId:guid}/deactivate")]
    public async Task<ActionResult<MissionStageResponse>> Deactivate(
        Guid missionStageId,
        CancellationToken cancellationToken)
    {
        var stage = await sender.Send(new DeactivateMissionStageCommand(missionStageId), cancellationToken);
        return Ok(stage);
    }
}
