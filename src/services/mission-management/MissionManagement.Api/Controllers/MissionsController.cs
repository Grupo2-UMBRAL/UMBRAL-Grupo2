using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
[Tags("Missions")]
public sealed class MissionsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List reusable missions")]
    [EndpointDescription("Returns the reusable Mission templates available for administration.")]
    [ProducesResponseType(typeof(IReadOnlyList<MissionSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<MissionSummaryResponse>>> List(CancellationToken cancellationToken)
    {
        var missions = await sender.Send(new ListMissionsQuery(), cancellationToken);
        return Ok(missions);
    }

    [HttpGet("{missionId:guid}")]
    [EndpointSummary("Get a reusable mission")]
    [EndpointDescription("Returns a Mission template with its ordered Path Items and playable Challenges.")]
    [ProducesResponseType(typeof(MissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MissionResponse>> GetById(Guid missionId, CancellationToken cancellationToken)
    {
        var mission = await sender.Send(new GetMissionByIdQuery(missionId), cancellationToken);
        return Ok(mission);
    }

    [HttpPost]
    [EndpointSummary("Create a reusable mission")]
    [EndpointDescription("Creates a Mission template with its ordered Sections and Challenges.")]
    [ProducesResponseType(typeof(MissionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MissionResponse>> Create(
        [FromBody] CreateMissionRequest request,
        CancellationToken cancellationToken)
    {
        var mission = await sender.Send(
            new CreateMissionCommand(
                request.Name,
                request.Description,
                request.MaximumDurationMinutes,
                request.Items),
            cancellationToken);

        return Created($"api/mission-management/missions/{mission.Id}", mission);
    }

    [HttpPut("{missionId:guid}")]
    [EndpointSummary("Update a reusable mission")]
    [EndpointDescription("Replaces the editable definition and ordered Path Items of a Mission template.")]
    [ProducesResponseType(typeof(MissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
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
                request.MaximumDurationMinutes,
                request.Items),
            cancellationToken);

        return Ok(mission);
    }

    [HttpPost("{missionId:guid}/activate")]
    [EndpointSummary("Activate a reusable mission")]
    [EndpointDescription("Makes a Mission available for creating a LiveSession.")]
    [ProducesResponseType(typeof(MissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MissionResponse>> Activate(Guid missionId, CancellationToken cancellationToken)
    {
        var mission = await sender.Send(new ActivateMissionCommand(missionId), cancellationToken);
        return Ok(mission);
    }

    [HttpPost("{missionId:guid}/deactivate")]
    [EndpointSummary("Deactivate a reusable mission")]
    [EndpointDescription("Prevents a Mission from being selected for new LiveSessions without deleting it.")]
    [ProducesResponseType(typeof(MissionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MissionResponse>> Deactivate(Guid missionId, CancellationToken cancellationToken)
    {
        var mission = await sender.Send(new DeactivateMissionCommand(missionId), cancellationToken);
        return Ok(mission);
    }
}
