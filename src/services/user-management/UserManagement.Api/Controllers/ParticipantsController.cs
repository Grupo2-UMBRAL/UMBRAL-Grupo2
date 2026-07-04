using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
public sealed class ParticipantsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateParticipant(
        [FromBody] CreateParticipantCommand request,
        CancellationToken cancellationToken)
    {
        var participant = await sender.Send(request, cancellationToken);

        return Created(
            $"/api/participants/{Uri.EscapeDataString(participant.Id)}",
            new { userId = participant.Id, username = participant.Username });
    }
}

public static class ParticipantSignupRateLimiter
{
    public const string PolicyName = "participant-signup";
}
