using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SessionManagement.Application.Features.EvidenceSubmissions;
using SessionManagement.Application.Features.SessionSnapshots;
using Umbral.ServiceDefaults;

namespace SessionManagement.Api.Controllers;

[ApiController]
[Route("api/session-management/session-teams")]
[Authorize]
[Tags("Session teams")]
public sealed class SessionTeamsController(ISender sender) : ControllerBase
{
    [HttpGet("{sessionTeamId:guid}/snapshot")]
    [Authorize(Roles = UmbralRoles.Participant)]
    [EndpointSummary("Get a session team snapshot")]
    [EndpointDescription("Returns the Participant Stage View for the current Play of a Session Team.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionTeamSnapshot>> GetSnapshot(
        Guid sessionTeamId,
        CancellationToken cancellationToken)
    {
        var snapshot = await sender.Send(new GetSessionTeamSnapshotQuery(sessionTeamId), cancellationToken);
        return Ok(snapshot);
    }

    [HttpPost("{sessionTeamId:guid}/submissions")]
    [Authorize(Roles = UmbralRoles.Participant)]
    [EndpointSummary("Submit Treasure Hunt evidence")]
    [EndpointDescription("Submits QR evidence for the current Treasure Hunt Play of a Session Team.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmitEvidenceResponse>> SubmitEvidence(
        Guid sessionTeamId,
        [FromBody] SubmitEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SubmitEvidenceCommand(sessionTeamId, request.QrHash), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{sessionTeamId:guid}/trivia-submissions")]
    [Authorize(Roles = UmbralRoles.Participant)]
    [EndpointSummary("Submit a Trivia answer")]
    [EndpointDescription("Submits the selected choice for the current Trivia Play of a Session Team.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmitEvidenceResponse>> SubmitTriviaAnswer(
        Guid sessionTeamId,
        [FromBody] SubmitTriviaAnswerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SubmitTriviaAnswerCommand(sessionTeamId, request.SelectedChoiceId), cancellationToken);
        return Ok(result);
    }
}
