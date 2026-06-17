using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbral.ServiceDefaults;
using UserManagement.Application.Features.Operators.Commands.CreateOperator;
using UserManagement.Application.Features.Operators.Commands.DeactivateOperator;
using UserManagement.Application.Features.Operators.Commands.RotateOperatorPassword;
using UserManagement.Application.Features.Operators.Queries.ListOperators;

namespace UserManagement.Api.Controllers;

[ApiController]
[Route("api/operators")]
[Authorize(Policy = UmbralRoles.Administrator)]
public sealed class OperatorsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListOperators(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListOperatorsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOperator(
        [FromBody] CreateOperatorCommand request,
        CancellationToken cancellationToken)
    {
        var operatorUser = await sender.Send(request, cancellationToken);
        
        return Created($"/api/operators/{Uri.EscapeDataString(operatorUser.Id)}", operatorUser);
    }

    [HttpPost("{userId}/deactivate")]
    public async Task<IActionResult> DeactivateOperator(
        string userId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeactivateOperatorCommand(userId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{userId}/reset-password")]
    public async Task<IActionResult> ResetPassword(
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

public sealed record RotateOperatorPasswordRequest(string? Password);
