using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UserManagement.Application.Common.Dtos;
using UserManagement.Application.Features.Participants.Commands.CreateParticipant;

namespace UserManagement.Api.Controllers;

/// <summary>
/// Public participant self-registration facade. Unlike <see cref="OperatorsController"/> this
/// endpoint is anonymous (players have no token yet) and rate limited to blunt abuse. Creation and
/// role assignment still happen server-side through the Keycloak admin client.
/// </summary>
[ApiController]
[Route("api/participants")]
[AllowAnonymous]
[EnableRateLimiting(ParticipantSignupRateLimiter.PolicyName)]
[Tags("Participants")]
public sealed class ParticipantsController(ISender sender) : ControllerBase
{
    [HttpPost]
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
}

public static class ParticipantSignupRateLimiter
{
    public const string PolicyName = "participant-signup";
}
