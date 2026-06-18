using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SessionManagement.Application.Features.EvidenceSubmissions;
using Umbral.ServiceDefaults;

namespace SessionManagement.Api.Controllers;

[ApiController]
[Route("api/session-management/submissions")]
[Authorize(Roles = $"{UmbralRoles.Administrator},{UmbralRoles.Operator}")]
public sealed class SubmissionsController(ISender sender) : ControllerBase
{
    [HttpPost("{submissionId:guid}/override")]
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
