using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SessionManagement.Application.Features.EvidenceSubmissions;
using Umbral.ServiceDefaults;

namespace SessionManagement.Api.Controllers;

[ApiController]
[Route("api/session-management/submissions")]
[Authorize(Roles = $"{UmbralRoles.Administrator},{UmbralRoles.Operator}")]
[Tags("Evidence submissions")]
public sealed class SubmissionsController(ISender sender) : ControllerBase
{
    [HttpPost("{submissionId:guid}/override")]
    [EndpointSummary("Override a validation outcome")]
    [EndpointDescription("Records an operator Validation Override for an ambiguous Evidence Submission.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OverrideValidationOutcomeResponse>> Override(
        Guid submissionId,
        [FromBody] OverrideValidationOutcomeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new OverrideValidationOutcomeCommand(submissionId, request.IsAccepted, request.Reason),
            cancellationToken);
        return Ok(result);
    }
}
