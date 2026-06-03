using MediatR;
using Microsoft.AspNetCore.Mvc;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Application.EvidenceSubmissions;
using Umbral.SessionOperations.Api.Application.Hints;
using Umbral.SessionOperations.Api.Application.LiveSessions;
using Umbral.SessionOperations.Api.Application.SessionLifecycle;
using Umbral.SessionOperations.Api.Application.SessionSnapshots;

namespace Umbral.SessionOperations.Api.Presentation;

public static class LiveSessionEndpointRouteBuilderExtensions
{
    public static RouteGroupBuilder MapLiveSessionRoutes(this RouteGroupBuilder authorizedApi)
    {
        ArgumentNullException.ThrowIfNull(authorizedApi);

        var liveSessionRoutes = authorizedApi
            .MapGroup("/live-sessions")
            .RequireAuthorization(policy => policy.RequireRole(UmbralRoles.Administrator, UmbralRoles.Operator));

        liveSessionRoutes.MapGet(
            "/",
            async (ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ListLiveSessionsQuery(), cancellationToken)));

        liveSessionRoutes.MapGet(
            "/{liveSessionId:guid}",
            async (Guid liveSessionId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetLiveSessionByIdQuery(liveSessionId), cancellationToken)));

        liveSessionRoutes.MapGet(
            "/{liveSessionId:guid}/overview",
            async (Guid liveSessionId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetLiveSessionOverviewQuery(liveSessionId), cancellationToken)));

        liveSessionRoutes.MapPost(
            "/",
            async (
                [FromBody] CreateLiveSessionRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var liveSession = await sender.Send(
                    new CreateLiveSessionCommand(
                        request.MissionId,
                        request.Name,
                        request.ScheduledStartAtUtc,
                        request.SelectedMissionStageIds),
                    cancellationToken);

                return Results.Created($"/api/session-operations/live-sessions/{liveSession.Id}", liveSession);
            });

        liveSessionRoutes.MapPost(
            "/{liveSessionId:guid}/lifecycle/start",
            async (Guid liveSessionId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new TransitionLiveSessionStateCommand(liveSessionId, LiveSessionLifecycleAction.Start),
                    cancellationToken)));

        liveSessionRoutes.MapPost(
            "/{liveSessionId:guid}/lifecycle/pause",
            async (Guid liveSessionId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new TransitionLiveSessionStateCommand(liveSessionId, LiveSessionLifecycleAction.Pause),
                    cancellationToken)));

        liveSessionRoutes.MapPost(
            "/{liveSessionId:guid}/lifecycle/resume",
            async (Guid liveSessionId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new TransitionLiveSessionStateCommand(liveSessionId, LiveSessionLifecycleAction.Resume),
                    cancellationToken)));

        liveSessionRoutes.MapPost(
            "/{liveSessionId:guid}/lifecycle/finalize",
            async (Guid liveSessionId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new TransitionLiveSessionStateCommand(liveSessionId, LiveSessionLifecycleAction.Finalize),
                    cancellationToken)));

        liveSessionRoutes.MapPost(
            "/{liveSessionId:guid}/lifecycle/cancel",
            async (Guid liveSessionId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new TransitionLiveSessionStateCommand(liveSessionId, LiveSessionLifecycleAction.Cancel),
                    cancellationToken)));
        liveSessionRoutes.MapPost(
            "/{liveSessionId:guid}/stages/{missionStageId:guid}/deactivate",
            async (Guid liveSessionId, Guid missionStageId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new DeactivateStageCommand(liveSessionId, missionStageId), cancellationToken)));

        liveSessionRoutes.MapPost(
            "/{liveSessionId:guid}/stages/{missionStageId:guid}/hints",
            async (
                Guid liveSessionId,
                Guid missionStageId,
                [FromBody] CreateOperationalHintRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Created(
                    $"/api/session-operations/live-sessions/{liveSessionId}/stages/{missionStageId}/hints",
                    await sender.Send(
                        new CreateOperationalHintCommand(
                            liveSessionId,
                            missionStageId,
                            request.Content,
                            request.Latitude,
                            request.Longitude),
                        cancellationToken)));

        liveSessionRoutes.MapPost(
            "/{liveSessionId:guid}/hints/{hintId:guid}/release",
            async (
                Guid liveSessionId,
                Guid hintId,
                [FromBody] ReleaseHintRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new ReleaseHintCommand(liveSessionId, request.SessionTeamId, hintId),
                    cancellationToken)));

        return authorizedApi;
    }
}
