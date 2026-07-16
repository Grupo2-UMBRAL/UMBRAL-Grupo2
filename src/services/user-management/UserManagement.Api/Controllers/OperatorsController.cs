using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbral.ServiceDefaults;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Features.Operators.Commands.CreateOperator;
using UserManagement.Application.Features.Operators.Commands.DeactivateOperator;
using UserManagement.Application.Features.Operators.Commands.RotateOperatorPassword;
using UserManagement.Application.Features.Operators.Queries.ListOperators;

namespace UserManagement.Api.Controllers;

[ApiController]
[Route("api/operators")]
[Authorize(Policy = UmbralRoles.Administrator)]
[Tags("Operators")]
public sealed class OperatorsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List operators")]
    [EndpointDescription("Returns the Operators managed by Keycloak.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<OperatorDto>>> ListOperators(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListOperatorsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [EndpointSummary("Create an operator")]
    [EndpointDescription("Creates an Operator in Keycloak and assigns its platform role.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OperatorDto>> CreateOperator(
        [FromBody] CreateOperatorCommand request,
        CancellationToken cancellationToken)
    {
        var operatorUser = await sender.Send(request, cancellationToken);
        
        return Created($"/api/operators/{Uri.EscapeDataString(operatorUser.Id)}", operatorUser);
    }

    [HttpPost("{userId}/deactivate")]
    [EndpointSummary("Deactivate an operator")]
    [EndpointDescription("Deactivates an Operator in Keycloak.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperatorDto>> DeactivateOperator(
        string userId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeactivateOperatorCommand(userId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{userId}/reset-password")]
    [EndpointSummary("Reset an operator password")]
    [EndpointDescription("Rotates the password of an Operator in Keycloak.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperatorDto>> ResetPassword(
        string userId,
        [FromBody] RotateOperatorPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RotateOperatorPasswordCommand(userId, request.Password),
            cancellationToken);
            
        return Ok(result);
    }
}

/// <summary>
/// Body of the operator password rotation request.
/// </summary>
/// <param name="Password">The new password. Required, 8 to 128 characters, trimmed before use;
/// null or blank is rejected with 400. Nullable here only so the omitted value surfaces as a
/// validation error instead of a deserialization failure.</param>
public sealed record RotateOperatorPasswordRequest(string? Password);
