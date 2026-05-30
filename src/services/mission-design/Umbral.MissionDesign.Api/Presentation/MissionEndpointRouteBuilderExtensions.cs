using MediatR;
using Microsoft.AspNetCore.Mvc;
using Umbral.MissionDesign.Api.Application.Missions;
using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Presentation;

public static class MissionEndpointRouteBuilderExtensions
{
    public static RouteGroupBuilder MapMissionRoutes(this RouteGroupBuilder authorizedApi)
    {
        ArgumentNullException.ThrowIfNull(authorizedApi);

        var missionRoutes = authorizedApi
            .MapGroup("/missions")
            .RequireAuthorization(UmbralAuthorizationPolicies.Administrator);

        missionRoutes.MapGet(
            "/",
            async (ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ListMissionsQuery(), cancellationToken)));

        missionRoutes.MapGet(
            "/{missionId:guid}",
            async (Guid missionId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetMissionByIdQuery(missionId), cancellationToken)));

        missionRoutes.MapPost(
            "/",
            async (
                [FromBody] CreateMissionRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var mission = await sender.Send(
                    new CreateMissionCommand(
                        request.Name,
                        request.Description,
                        request.Difficulty,
                        request.MaximumDurationMinutes,
                        request.GameType),
                    cancellationToken);

                return Results.Created($"/api/mission-design/missions/{mission.Id}", mission);
            });

        missionRoutes.MapPut(
            "/{missionId:guid}",
            async (
                Guid missionId,
                [FromBody] UpdateMissionRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(
                    await sender.Send(
                        new UpdateMissionCommand(
                            missionId,
                            request.Name,
                            request.Description,
                            request.Difficulty,
                            request.MaximumDurationMinutes),
                        cancellationToken)));

        missionRoutes.MapPost(
            "/{missionId:guid}/deactivate",
            async (Guid missionId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new DeactivateMissionCommand(missionId), cancellationToken)));

        return authorizedApi;
    }
}
