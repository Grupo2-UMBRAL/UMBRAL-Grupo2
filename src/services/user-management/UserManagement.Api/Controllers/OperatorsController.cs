using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbral.ServiceDefaults;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Features.Operators.Commands.CreateOperator;
using UserManagement.Application.Features.Operators.Commands.ActivateOperator;
using UserManagement.Application.Features.Operators.Commands.DeactivateOperator;
using UserManagement.Application.Features.Operators.Commands.ResendOperatorInvitation;
using UserManagement.Application.Features.Operators.Commands.SendOperatorPasswordResetLink;
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

    [HttpPost("{userId}/activate")]
    [EndpointSummary("Reactivate an operator")]
    [EndpointDescription("Reactivates a deactivated Operator in Keycloak.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperatorDto>> ActivateOperator(string userId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ActivateOperatorCommand(userId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{userId}/resend-onboarding-invitation")]
    [EndpointSummary("Resend an operator onboarding invitation")]
    [EndpointDescription("Re-issues the Keycloak onboarding email (set password, complete profile, verify email) for an existing Operator.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperatorDto>> ResendOnboardingInvitation(
        string userId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ResendOperatorInvitationCommand(userId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{userId}/send-password-reset-link")]
    [EndpointSummary("Send an operator password reset link")]
    [EndpointDescription("Asks Keycloak to email an Operator a link to choose a new password.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperatorDto>> SendPasswordResetLink(
        string userId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SendOperatorPasswordResetLinkCommand(userId), cancellationToken);
        return Ok(result);
    }
}
