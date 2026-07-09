using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Umbral.ServiceDefaults;

/// <summary>
/// Cross-cutting MediatR pipeline behavior that records the name, elapsed time, and outcome of every
/// request flowing through a service's Application layer (RNF-08 technical/diagnostic logging).
/// Emits only to <see cref="ILogger"/> (surfaced through OpenTelemetry), so it stays local to the
/// process: it never calls another service nor publishes to RabbitMQ. Business/audit history
/// (session event log) is a separate domain concern and deliberately NOT handled here.
/// </summary>
/// <remarks>
/// Registered once per service via <c>AddOpenBehavior(typeof(UmbralLoggingBehavior&lt;,&gt;))</c>.
/// A single shared implementation lives in <c>Umbral.ServiceDefaults</c> (ADR-012: shared kernel
/// holds technical cross-cutting concerns only) instead of one copy per service.
/// </remarks>
public sealed class UmbralLoggingBehavior<TRequest, TResponse>(
    ILogger<UmbralLoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName}", requestName);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await next();
            stopwatch.Stop();
            logger.LogInformation(
                "Handled {RequestName} in {ElapsedMilliseconds}ms",
                requestName, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (UmbralServiceException exception)
        {
            // Expected, categorized failures (validation/not-found/conflict/…): domain outcome, not a
            // defect — log at Warning with the category so it stays diagnosable without paging noise.
            stopwatch.Stop();
            logger.LogWarning(
                exception,
                "{RequestName} failed ({FailureCategory}: {FailureCode}) after {ElapsedMilliseconds}ms",
                requestName, exception.Category, exception.Code, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            // Unhandled/unexpected: genuine error worth an alert.
            stopwatch.Stop();
            logger.LogError(
                exception,
                "{RequestName} threw an unhandled exception after {ElapsedMilliseconds}ms",
                requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
