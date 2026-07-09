using Umbral.ServiceDefaults;

namespace UserManagement.Application.Features.Operators.Commands.DeactivateOperator;

/// <summary>
/// Static, throw-on-failure validator for <see cref="DeactivateOperatorCommand"/> — mirrors the
/// manual validation pattern used across user-management (no FluentValidation).
/// </summary>
public static class DeactivateOperatorCommandValidator
{
    public static void Validate(DeactivateOperatorCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new UmbralDomainException(
                "operator_user_id_required",
                "Operator user id is required.",
                UmbralFailureCategory.Validation);
        }
    }
}
