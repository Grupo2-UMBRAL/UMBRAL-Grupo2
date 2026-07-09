using Umbral.ServiceDefaults;
using UserManagement.Application.Common;

namespace UserManagement.Application.Features.Operators.Commands.RotateOperatorPassword;

/// <summary>
/// Static, throw-on-failure validator for <see cref="RotateOperatorPasswordCommand"/>. Enforces a
/// non-blank user id and delegates the password rules to the shared <see cref="PasswordPolicy"/> so
/// they stay identical to create-operator (codes <c>operator_password_*</c>).
/// </summary>
public static class RotateOperatorPasswordCommandValidator
{
    public static void Validate(RotateOperatorPasswordCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new UmbralDomainException(
                "operator_user_id_required",
                "Operator user id is required.",
                UmbralFailureCategory.Validation);
        }

        PasswordPolicy.Normalize(request.Password, "operator_password");
    }
}
