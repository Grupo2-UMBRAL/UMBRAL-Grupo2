using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SessionManagement.Infrastructure;
using Umbral.ServiceDefaults;
using Xunit;

namespace SessionManagement.IntegrationTests.Infrastructure;

public sealed class HttpContextIdentityTests
{
    private static HttpContextAccessor AccessorWithClaims(params Claim[] claims)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
        };
        return new HttpContextAccessor { HttpContext = context };
    }

    private static HttpContextAccessor AnonymousAccessor()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        return new HttpContextAccessor { HttpContext = context };
    }

    [Fact]
    public void GetRequiredOperatorUserId_NameIdentifierClaim_ReturnsValue()
    {
        var identity = new HttpContextCurrentOperatorIdentity(
            AccessorWithClaims(new Claim(ClaimTypes.NameIdentifier, "op-name-id")));

        Assert.Equal("op-name-id", identity.GetRequiredOperatorUserId());
    }

    [Fact]
    public void GetRequiredOperatorUserId_SubClaim_ReturnsValue()
    {
        var identity = new HttpContextCurrentOperatorIdentity(
            AccessorWithClaims(new Claim("sub", "op-sub")));

        Assert.Equal("op-sub", identity.GetRequiredOperatorUserId());
    }

    [Fact]
    public void GetRequiredOperatorUserId_TrimsWhitespace()
    {
        var identity = new HttpContextCurrentOperatorIdentity(
            AccessorWithClaims(new Claim("sub", "  op-trimmed  ")));

        Assert.Equal("op-trimmed", identity.GetRequiredOperatorUserId());
    }

    [Fact]
    public void GetRequiredOperatorUserId_MissingClaim_ThrowsUnauthorizedDomainException()
    {
        var identity = new HttpContextCurrentOperatorIdentity(AnonymousAccessor());

        var exception = Assert.Throws<UmbralDomainException>(() => identity.GetRequiredOperatorUserId());

        Assert.Equal("operator_identity_required", exception.Code);
        Assert.Equal(UmbralFailureCategory.Unauthorized, exception.Category);
    }

    [Fact]
    public void GetRequiredParticipantUserId_NameIdentifierClaim_ReturnsParsedValue()
    {
        var identity = new HttpContextCurrentParticipantIdentity(
            AccessorWithClaims(new Claim(ClaimTypes.NameIdentifier, "participant-name-id")));

        var participantUserId = identity.GetRequiredParticipantUserId();

        Assert.Equal("participant-name-id", participantUserId.Value);
    }

    [Fact]
    public void GetRequiredParticipantUserId_SubClaim_ReturnsParsedValue()
    {
        var identity = new HttpContextCurrentParticipantIdentity(
            AccessorWithClaims(new Claim("sub", "participant-sub")));

        var participantUserId = identity.GetRequiredParticipantUserId();

        Assert.Equal("participant-sub", participantUserId.Value);
    }

    [Fact]
    public void GetRequiredParticipantUserId_MissingClaim_ThrowsUnauthorizedDomainException()
    {
        var identity = new HttpContextCurrentParticipantIdentity(AnonymousAccessor());

        var exception = Assert.Throws<UmbralDomainException>(() => identity.GetRequiredParticipantUserId());

        Assert.Equal("participant_identity_required", exception.Code);
        Assert.Equal(UmbralFailureCategory.Unauthorized, exception.Category);
    }

    [Fact]
    public void GetRequiredParticipantUserId_ValueTooLong_ThrowsValidationDomainException()
    {
        // ParticipantUserId.Parse rejects values over MaximumLength (120) with a Validation failure.
        var oversized = new string('p', 121);
        var identity = new HttpContextCurrentParticipantIdentity(
            AccessorWithClaims(new Claim("sub", oversized)));

        var exception = Assert.Throws<UmbralDomainException>(() => identity.GetRequiredParticipantUserId());

        Assert.Equal("participant_user_id_too_long", exception.Code);
        Assert.Equal(UmbralFailureCategory.Validation, exception.Category);
    }
}
