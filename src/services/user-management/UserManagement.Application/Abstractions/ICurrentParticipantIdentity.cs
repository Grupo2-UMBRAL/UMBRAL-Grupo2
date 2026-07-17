namespace UserManagement.Application.Abstractions;

/// <summary>
/// Resolves the Participant behind the current request from its token. Handlers depend on this
/// rather than on the claim itself, so no route or body value ever chooses which account is read or
/// written -- the reason the self-service endpoints cannot be turned into an IDOR.
/// </summary>
/// <remarks>
/// Returns a plain <see cref="string"/>: unlike the session-management counterpart, this service has
/// no Domain project to hold an identifier type (see CONTEXT.md).
/// </remarks>
public interface ICurrentParticipantIdentity
{
    string GetRequiredParticipantUserId();
}
