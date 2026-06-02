using MediatR;
using Microsoft.AspNetCore.Mvc;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Application.LiveSessions;

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

        return authorizedApi;
    }
}
