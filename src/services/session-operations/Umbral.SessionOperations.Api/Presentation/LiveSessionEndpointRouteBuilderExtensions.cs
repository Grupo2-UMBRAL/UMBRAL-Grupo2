using MediatR;
using Microsoft.AspNetCore.Mvc;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Application.EvidenceSubmissions;
using Umbral.SessionOperations.Api.Application.Hints;
using Umbral.SessionOperations.Api.Application.LiveSessions;
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
            "/{liveSessionId:guid}/hints/{hintId:guid}/release",
            async (
                Guid liveSessionId,
                Guid hintId,
                [FromBody] ReleaseHintRequest? request,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new ReleaseHintCommand(liveSessionId, request?.SessionTeamId, hintId),
                    cancellationToken)));

        liveSessionRoutes.MapPost(
            "/{liveSessionId:guid}/stages/{missionStageId:guid}/hints",
            async (
                Guid liveSessionId,
                Guid missionStageId,
                [FromBody] CreateOperationalHintRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new CreateOperationalHintCommand(
                        liveSessionId,
                        missionStageId,
                        request.Content,
                        request.Latitude,
                        request.Longitude),
                    cancellationToken)));

        var participantSnapshotRoutes = authorizedApi
            .MapGroup("/session-teams")
            .RequireAuthorization(policy => policy.RequireRole(UmbralRoles.Participant));

        participantSnapshotRoutes.MapGet(
            "/{sessionTeamId:guid}/snapshot",
            async (Guid sessionTeamId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetSessionTeamSnapshotQuery(sessionTeamId), cancellationToken)));

        participantSnapshotRoutes.MapPost(
            "/{sessionTeamId:guid}/submissions",
            async (
                Guid sessionTeamId,
                [FromBody] SubmitEvidenceRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new SubmitEvidenceCommand(sessionTeamId, request.QrHash),
                    cancellationToken)));

        participantSnapshotRoutes.MapPost(
            "/{sessionTeamId:guid}/trivia-submissions",
            async (
                Guid sessionTeamId,
                [FromBody] SubmitTriviaAnswerRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new SubmitTriviaAnswerCommand(sessionTeamId, request.AnswerText),
                    cancellationToken)));

        var evidenceSubmissionRoutes = authorizedApi
            .MapGroup("/submissions")
            .RequireAuthorization(policy => policy.RequireRole(UmbralRoles.Administrator, UmbralRoles.Operator));

        evidenceSubmissionRoutes.MapPost(
            "/{evidenceSubmissionId:guid}/override",
            async (
                Guid evidenceSubmissionId,
                [FromBody] OverrideValidationOutcomeRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new OverrideValidationOutcomeCommand(evidenceSubmissionId, request.IsAccepted, request.Reason),
                    cancellationToken)));

        return authorizedApi;
    }
}
