using System.Security.Claims;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Features.EvidenceSubmissions;

namespace SessionManagement.Infrastructure;

public sealed class HttpContextCurrentOperatorIdentity(IHttpContextAccessor httpContextAccessor)
    : ICurrentOperatorIdentity
{
    public string GetRequiredOperatorUserId()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal?.FindFirstValue("sub");

        if (!string.IsNullOrWhiteSpace(userId))
        {
            return userId.Trim();
        }

        throw new UmbralDomainException(
            "operator_identity_required",
            "Authenticated operator identity is required for Validation Override.",
            UmbralFailureCategory.Unauthorized);
    }
}
