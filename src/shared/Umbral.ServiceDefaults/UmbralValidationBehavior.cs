using FluentValidation;
using MediatR;

namespace Umbral.ServiceDefaults;

/// <summary>
/// Cross-cutting MediatR pipeline behavior that runs the FluentValidation validators registered for
/// a request before it reaches its handler, so handlers only ever see structurally valid input (SRP).
/// </summary>
/// <remarks>
/// Registered once per service via <c>AddOpenBehavior(typeof(UmbralValidationBehavior&lt;,&gt;))</c>,
/// AFTER <see cref="UmbralLoggingBehavior{TRequest,TResponse}"/> so a validation failure is still
/// logged as a categorized warning by the outer logging behavior.
///
/// Fail-fast, single code: on the first failing validator the first failure is surfaced as an
/// <see cref="UmbralDomainException"/> with <see cref="UmbralFailureCategory.Validation"/>, carrying
/// the rule's <c>ErrorCode</c>. This keeps the HTTP problem-details contract (code + category)
/// identical to the previous throw-on-first-failure manual validators. Validators therefore use
/// <see cref="CascadeMode.Stop"/> so <c>Errors[0]</c> is the first rule that failed, in declared order.
/// </remarks>
public sealed class UmbralValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            if (result.IsValid)
            {
                continue;
            }

            var failure = result.Errors[0];
            throw new UmbralDomainException(
                string.IsNullOrWhiteSpace(failure.ErrorCode) ? "validation_failed" : failure.ErrorCode,
                failure.ErrorMessage,
                UmbralFailureCategory.Validation);
        }

        return await next();
    }
}
