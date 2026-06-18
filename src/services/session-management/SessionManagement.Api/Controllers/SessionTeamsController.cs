using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SessionManagement.Application.Features.EvidenceSubmissions;
using SessionManagement.Application.Features.SessionSnapshots;
using Umbral.ServiceDefaults;

namespace SessionManagement.Api.Controllers;

[ApiController]
[Route("api/session-management/session-teams")]
[Authorize]
public sealed class SessionTeamsController(ISender sender) : ControllerBase
{
    [HttpGet("{sessionTeamId:guid}/snapshot")]
    [Authorize(Roles = UmbralRoles.Participant)]
    public async Task<ActionResult<SessionTeamSnapshot>> GetSnapshot(
        Guid sessionTeamId,
        CancellationToken cancellationToken)
    {
        var snapshot = await sender.Send(new GetSessionTeamSnapshotQuery(sessionTeamId), cancellationToken);
        return Ok(snapshot);
    }

    [HttpPost("{sessionTeamId:guid}/submissions")]
    [Authorize(Roles = UmbralRoles.Participant)]
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
    public async Task<ActionResult<SubmitEvidenceResponse>> SubmitTriviaAnswer(
        Guid sessionTeamId,
        [FromBody] SubmitTriviaAnswerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new SubmitTriviaAnswerCommand(sessionTeamId, request.AnswerText), cancellationToken);
        return Ok(result);
    }
}
