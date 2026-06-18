using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MissionManagement.Application.Features.MissionStages.Commands.CreateMissionStage;
using MissionManagement.Application.Features.MissionStages.Queries.ListMissionStages;
using MissionManagement.Application.Features.Missions.Commands.ActivateMission;
using MissionManagement.Application.Features.Missions.Commands.CreateMission;
using MissionManagement.Application.Features.Missions.Commands.DeactivateMission;
using MissionManagement.Application.Features.Missions.Commands.UpdateMission;
using MissionManagement.Application.Features.Missions.Queries.GetMissionById;
using MissionManagement.Application.Features.Missions.Queries.ListMissions;
using Umbral.ServiceDefaults;

namespace MissionManagement.Api.Controllers;

[ApiController]
[Route("api/mission-management/missions")]
[Authorize(Roles = UmbralRoles.Administrator)]
public sealed class MissionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MissionSummaryResponse>>> List(CancellationToken cancellationToken)
    {
        var missions = await sender.Send(new ListMissionsQuery(), cancellationToken);
        return Ok(missions);
    }

    [HttpGet("{missionId:guid}")]
    public async Task<ActionResult<MissionResponse>> GetById(Guid missionId, CancellationToken cancellationToken)
    {
        var mission = await sender.Send(new GetMissionByIdQuery(missionId), cancellationToken);
        return Ok(mission);
    }

    [HttpPost]
    public async Task<ActionResult<MissionResponse>> Create(
        [FromBody] CreateMissionRequest request,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new CreateMissionCommand(
                request.Name,
                request.Description,
                request.Difficulty,
                request.MaximumDurationMinutes,
                request.GameType,
                request.Nodes),
            cancellationToken);

        return Created($"api/mission-management/missions/{mission.Id}", mission);
    }

    [HttpPut("{missionId:guid}")]
    public async Task<ActionResult<MissionResponse>> Update(
        Guid missionId,
        [FromBody] UpdateMissionRequest request,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new UpdateMissionCommand(
                missionId,
                request.Name,
                request.Description,
                request.Difficulty,
                request.MaximumDurationMinutes,
                request.GameType,
                request.Nodes),
            cancellationToken);

        return Ok(mission);
    }

    [HttpPost("{missionId:guid}/activate")]
    public async Task<ActionResult<MissionResponse>> Activate(Guid missionId, CancellationToken cancellationToken)
    {
        var mission = await sender.Send(new ActivateMissionCommand(missionId), cancellationToken);
        return Ok(mission);
    }

    [HttpPost("{missionId:guid}/deactivate")]
    public async Task<ActionResult<MissionResponse>> Deactivate(Guid missionId, CancellationToken cancellationToken)
    {
        var mission = await sender.Send(new DeactivateMissionCommand(missionId), cancellationToken);
        return Ok(mission);
    }

    [HttpGet("{missionId:guid}/stages")]
    public async Task<ActionResult<IReadOnlyList<MissionStageSummaryResponse>>> ListStages(
        Guid missionId,
        CancellationToken cancellationToken)
    {
        var stages = await sender.Send(new ListMissionStagesQuery(missionId), cancellationToken);
        return Ok(stages);
    }

    [HttpPost("{missionId:guid}/stages")]
    public async Task<ActionResult<MissionStageResponse>> CreateStage(
        Guid missionId,
        [FromBody] CreateMissionStageRequest request,
        CancellationToken cancellationToken)
    {
        var stage = await sender.Send(
            new CreateMissionStageCommand(
                missionId,
                request.Name,
                request.Order,
                request.Difficulty,
                request.GameType,
                request.ExpectedQrHash,
                request.TriviaValidationCriteria),
            cancellationToken);

        return Created($"api/mission-management/stages/{stage.Id}", stage);
    }
}
