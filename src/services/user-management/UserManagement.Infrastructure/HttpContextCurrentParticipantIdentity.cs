using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Umbral.ServiceDefaults;
using UserManagement.Application.Abstractions;

namespace UserManagement.Infrastructure;

/// <summary>
/// Reads the Participant id from the bearer token. Mirrors the session-management adapter of the
/// same name, including the <c>sub</c> fallback: the JWT handler renames that claim to
/// <see cref="ClaimTypes.NameIdentifier"/>, but only when the inbound claim map is left at its
/// default.
/// </summary>
public sealed class HttpContextCurrentParticipantIdentity(IHttpContextAccessor httpContextAccessor)
    : ICurrentParticipantIdentity
{
    public string GetRequiredParticipantUserId()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal?.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UmbralDomainException(
                "participant_identity_required",
                "Authenticated participant identity is required for self-service.",
                UmbralFailureCategory.Unauthorized);
        }

        return userId.Trim();
    }
}
