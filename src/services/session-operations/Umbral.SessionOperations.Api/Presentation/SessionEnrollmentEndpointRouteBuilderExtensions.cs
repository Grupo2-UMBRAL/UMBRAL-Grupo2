using MediatR;
using Microsoft.AspNetCore.Mvc;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Application.SessionEnrollment;
using Umbral.SessionOperations.Api.Application.SessionSnapshots;

namespace Umbral.SessionOperations.Api.Presentation;

public static class SessionEnrollmentEndpointRouteBuilderExtensions
{
    public static RouteGroupBuilder MapSessionEnrollmentRoutes(this RouteGroupBuilder authorizedApi)
    {
        ArgumentNullException.ThrowIfNull(authorizedApi);

        var operatorRoutes = authorizedApi
            .MapGroup("/live-sessions/{liveSessionId:guid}/session-enrollment")
            .RequireAuthorization(policy => policy.RequireRole(UmbralRoles.Administrator, UmbralRoles.Operator));

        operatorRoutes.MapPost(
            "/join-code",
            async (Guid liveSessionId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GenerateJoinCodeCommand(liveSessionId), cancellationToken)));

        operatorRoutes.MapPost(
            "/window/open",
            async (Guid liveSessionId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new OpenEnrollmentWindowCommand(liveSessionId), cancellationToken)));

        operatorRoutes.MapPost(
            "/window/close",
            async (Guid liveSessionId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new CloseEnrollmentWindowCommand(liveSessionId), cancellationToken)));

        var participantRoutes = authorizedApi
            .MapGroup("/session-enrollment")
            .RequireAuthorization(policy => policy.RequireRole(UmbralRoles.Participant));

        var participantSnapshotRoutes = authorizedApi
            .MapGroup("/session-teams")
            .RequireAuthorization(policy => policy.RequireRole(UmbralRoles.Participant));

        participantRoutes.MapGet(
            "/{joinCode}/validate",
            async (string joinCode, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ValidateJoinCodeQuery(joinCode), cancellationToken)));

        participantRoutes.MapGet(
            "/{joinCode}/teams",
            async (string joinCode, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ListSessionTeamsQuery(joinCode), cancellationToken)));

        participantRoutes.MapPost(
            "/teams",
            async (
                [FromBody] RegisterTeamRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new RegisterTeamCommand(request.JoinCode, request.TeamName), cancellationToken);
                return Results.Created(
                    $"/api/session-operations/live-sessions/{result.LiveSessionId}/session-teams/{result.SessionTeamId}",
                    result);
            });

        participantRoutes.MapPost(
            "/join",
            async (
                [FromBody] JoinSessionTeamRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new JoinSessionTeamCommand(request.JoinCode, request.SessionTeamId),
                    cancellationToken)));
        var participantSessionTeamRoutes = authorizedApi
            .MapGroup("/session-teams")
            .RequireAuthorization(policy => policy.RequireRole(UmbralRoles.Participant));

        participantSessionTeamRoutes.MapGet(
            "/{sessionTeamId:guid}/snapshot",
            async (Guid sessionTeamId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetSessionTeamSnapshotQuery(sessionTeamId), cancellationToken)));

        participantSnapshotRoutes.MapGet(
            "/{sessionTeamId:guid}/snapshot",
            async (Guid sessionTeamId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetSessionTeamSnapshotQuery(sessionTeamId), cancellationToken)));

        return authorizedApi;
    }
}
