using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Umbral.ServiceDefaults;

public sealed class UmbralExceptionHandler(ILogger<UmbralExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not UmbralServiceException serviceException)
        {
            return false;
        }

        var statusCode = MapStatusCode(serviceException.Category);
        logger.LogWarning(
            exception,
            "Handled {FailureCategory} exception with code {FailureCode}.",
            serviceException.Category,
            serviceException.Code);

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitle(serviceException.Category),
            Detail = serviceException.Message,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        problemDetails.Extensions["code"] = serviceException.Code;
        problemDetails.Extensions["category"] = serviceException.Category.ToString();

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static int MapStatusCode(UmbralFailureCategory category) =>
        category switch
        {
            UmbralFailureCategory.Validation => StatusCodes.Status400BadRequest,
            UmbralFailureCategory.Unauthorized => StatusCodes.Status401Unauthorized,
            UmbralFailureCategory.Forbidden => StatusCodes.Status403Forbidden,
            UmbralFailureCategory.NotFound => StatusCodes.Status404NotFound,
            UmbralFailureCategory.Conflict => StatusCodes.Status409Conflict,
            UmbralFailureCategory.Domain => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError
        };

    private static string GetTitle(UmbralFailureCategory category) =>
        category switch
        {
            UmbralFailureCategory.Validation => "Validation failure",
            UmbralFailureCategory.Unauthorized => "Authentication failure",
            UmbralFailureCategory.Forbidden => "Authorization failure",
            UmbralFailureCategory.NotFound => "Resource not found",
            UmbralFailureCategory.Conflict => "Conflict detected",
            UmbralFailureCategory.Domain => "Domain rule violated",
            _ => "Technical failure"
        };
}
