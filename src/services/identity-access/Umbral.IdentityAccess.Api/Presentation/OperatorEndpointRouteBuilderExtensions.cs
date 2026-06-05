using MediatR;
using Microsoft.AspNetCore.Mvc;
using Umbral.IdentityAccess.Api.Application.Operators;
using Umbral.ServiceDefaults;

namespace Umbral.IdentityAccess.Api.Presentation;

public static class OperatorEndpointRouteBuilderExtensions
{
    public static RouteGroupBuilder MapOperatorRoutes(this RouteGroupBuilder authorizedApi)
    {
        ArgumentNullException.ThrowIfNull(authorizedApi);

        var operatorRoutes = authorizedApi
            .MapGroup("/operators")
            .RequireAuthorization(UmbralAuthorizationPolicies.Administrator);

        operatorRoutes.MapGet(
            "/",
            async (ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ListOperatorsQuery(), cancellationToken)));

        operatorRoutes.MapPost(
            "/",
            async (
                [FromBody] CreateOperatorRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var operatorUser = await sender.Send(
                    new CreateOperatorCommand(
                        request.Username,
                        request.Email,
                        request.FirstName,
                        request.LastName,
                        request.Password),
                    cancellationToken);

                return Results.Created(
                    $"/api/identity-access/operators/{Uri.EscapeDataString(operatorUser.Id)}",
                    operatorUser);
            });

        operatorRoutes.MapPost(
            "/{userId}/deactivate",
            async (string userId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new DeactivateOperatorCommand(userId), cancellationToken)));

        operatorRoutes.MapPost(
            "/{userId}/reset-password",
            async (
                string userId,
                [FromBody] RotateOperatorPasswordRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(
                    new RotateOperatorPasswordCommand(userId, request.Password),
                    cancellationToken)));

        return authorizedApi;
    }
}

public sealed record CreateOperatorRequest(
    string? Username,
    string? Email,
    string? FirstName,
    string? LastName,
    string? Password);

public sealed record RotateOperatorPasswordRequest(string? Password);
