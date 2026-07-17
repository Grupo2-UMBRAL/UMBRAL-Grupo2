using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Umbral.ServiceDefaults;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Features.Participants.Commands.ChangeParticipantUsername;
using UserManagement.Application.Features.Participants.Commands.CreateParticipant;
using UserManagement.Application.Features.Participants.Commands.DeactivateParticipantAccount;
using UserManagement.Application.Features.Participants.Queries.GetParticipantProfile;

namespace UserManagement.Api.Controllers;

/// <summary>
/// Participant facade. Registration is anonymous (players have no token yet) and rate limited to
/// blunt abuse; both attributes are declared on that action alone, never on the class, so the
/// self-service actions alongside it stay authenticated and keep their own limiter budget. Creation
/// and role assignment still happen server-side through the Keycloak admin client.
/// </summary>
[ApiController]
[Route("api/participants")]
[Tags("Participants")]
public sealed class ParticipantsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(ParticipantSignupRateLimiter.PolicyName)]
    [EndpointSummary("Register a participant")]
    [EndpointDescription("Creates a Participant in Keycloak through the public, rate-limited registration flow.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ParticipantDto>> CreateParticipant(
        [FromBody] CreateParticipantCommand request,
        CancellationToken cancellationToken)
    {
        var participant = await sender.Send(request, cancellationToken);

        return Created(
            $"/api/participants/{Uri.EscapeDataString(participant.UserId)}",
            participant);
    }

    [HttpGet("me")]
    [Authorize(Policy = UmbralRoles.Participant)]
    [EndpointSummary("Read my participant profile")]
    [EndpointDescription("Returns the account of the authenticated Participant, resolved from the token.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ParticipantProfileDto>> GetMyProfile(CancellationToken cancellationToken)
    {
        var profile = await sender.Send(new GetParticipantProfileQuery(), cancellationToken);

        return Ok(profile);
    }

    [HttpPatch("me/username")]
    [Authorize(Policy = UmbralRoles.Participant)]
    [EnableRateLimiting(ParticipantUsernameRateLimiter.PolicyName)]
    [EndpointSummary("Change my username")]
    [EndpointDescription("Renames the authenticated Participant. The username is also their login handle.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ParticipantProfileDto>> ChangeMyUsername(
        [FromBody] ChangeParticipantUsernameCommand request,
        CancellationToken cancellationToken)
    {
        var profile = await sender.Send(request, cancellationToken);

        return Ok(profile);
    }

    [HttpPost("me/deactivate")]
    [Authorize(Policy = UmbralRoles.Participant)]
    [EndpointSummary("Deactivate my account")]
    [EndpointDescription(
        "Blocks the authenticated Participant from logging in again and revokes their sessions. " +
        "Irreversible from the app: only an administrator can re-enable the account.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeactivateMyAccount(CancellationToken cancellationToken)
    {
        await sender.Send(new DeactivateParticipantAccountCommand(), cancellationToken);

        return NoContent();
    }
}

public static class ParticipantSignupRateLimiter
{
    public const string PolicyName = "participant-signup";
}

public static class ParticipantUsernameRateLimiter
{
    public const string PolicyName = "participant-username-change";
}
