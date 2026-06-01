using MediatR;
using Microsoft.AspNetCore.Mvc;
using Umbral.MissionDesign.Api.Application.MissionStages;
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

        var liveSessionCandidateRoutes = authorizedApi
            .MapGroup("/missions/eligible-for-live-session")
            .RequireAuthorization(policy => policy.RequireRole(UmbralRoles.Administrator, UmbralRoles.Operator));

        missionRoutes.MapGet(
            "/",
            async (ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ListMissionsQuery(), cancellationToken)));

        liveSessionCandidateRoutes.MapGet(
            "/",
            async (ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ListEligibleMissionsForLiveSessionQuery(), cancellationToken)));

        liveSessionCandidateRoutes.MapGet(
            "/{missionId:guid}",
            async (Guid missionId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetEligibleMissionForLiveSessionQuery(missionId), cancellationToken)));

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
                        request.GameType,
                        request.Nodes),
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
                            request.MaximumDurationMinutes,
                            request.GameType,
                            request.Nodes),
                        cancellationToken)));

        missionRoutes.MapPost(
            "/{missionId:guid}/activate",
            async (Guid missionId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ActivateMissionCommand(missionId), cancellationToken)));

        missionRoutes.MapPost(
            "/{missionId:guid}/deactivate",
            async (Guid missionId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new DeactivateMissionCommand(missionId), cancellationToken)));

        missionRoutes.MapGet(
            "/{missionId:guid}/stages",
            async (Guid missionId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ListMissionStagesQuery(missionId), cancellationToken)));

        missionRoutes.MapPost(
            "/{missionId:guid}/stages",
            async (
                Guid missionId,
                [FromBody] CreateMissionStageRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var missionStage = await sender.Send(
                    new CreateMissionStageCommand(
                        missionId,
                        request.Name,
                        request.Order,
                        request.GameType,
                        request.ExpectedQrHash,
                        request.TriviaValidationCriteria),
                    cancellationToken);

                return Results.Created($"/api/mission-design/stages/{missionStage.Id}", missionStage);
            });

        var missionStageRoutes = authorizedApi
            .MapGroup("/stages")
            .RequireAuthorization(UmbralAuthorizationPolicies.Administrator);

        missionStageRoutes.MapGet(
            "/{missionStageId:guid}",
            async (Guid missionStageId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetMissionStageByIdQuery(missionStageId), cancellationToken)));

        missionStageRoutes.MapPost(
            "/{missionStageId:guid}/hints",
            async (
                Guid missionStageId,
                [FromBody] CreateMissionStageHintRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var hint = await sender.Send(
                    new CreateMissionStageHintCommand(
                        missionStageId,
                        request.Content,
                        request.IsSolution,
                        request.Latitude,
                        request.Longitude),
                    cancellationToken);

                return Results.Created($"/api/mission-design/stages/{missionStageId}", hint);
            });

        missionStageRoutes.MapPost(
            "/{missionStageId:guid}/deactivate",
            async (Guid missionStageId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new DeactivateMissionStageCommand(missionStageId), cancellationToken)));

        return authorizedApi;
    }
}
