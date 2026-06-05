using System.Security.Claims;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Application.SessionEnrollment;
using Umbral.SessionOperations.Api.Domain.LiveSessions;

namespace Umbral.SessionOperations.Api.Infrastructure;

public sealed class HttpContextCurrentParticipantIdentity(IHttpContextAccessor httpContextAccessor)
    : ICurrentParticipantIdentity
{
    public ParticipantUserId GetRequiredParticipantUserId()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal?.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UmbralDomainException(
                "participant_identity_required",
                "Authenticated participant identity is required for enrollment.",
                UmbralFailureCategory.Unauthorized);
        }

        return ParticipantUserId.Parse(userId);
    }
}
