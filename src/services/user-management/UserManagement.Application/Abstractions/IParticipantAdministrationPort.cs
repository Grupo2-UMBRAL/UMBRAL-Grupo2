using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Abstractions;

/// <summary>
/// Keycloak operations a Participant performs on their own account. Separate from
/// <see cref="IOperatorAdministrationPort"/> because the vocabularies are separate, even though one
/// adapter implements both: a Participant must never meet an <c>operator_*</c> failure code.
/// </summary>
public interface IParticipantAdministrationPort
{
    Task<ParticipantProfileDto> GetParticipantProfileAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// The id of the User currently holding <paramref name="username"/>, or <see langword="null"/> if
    /// it is free. Returns the id rather than a boolean because the holder may be the caller, and
    /// only the Application layer knows that renaming yourself to your own handle is not a conflict.
    /// </summary>
    Task<string?> FindUserIdByUsernameAsync(string username, CancellationToken cancellationToken);

    Task<ParticipantProfileDto> UpdateUsernameAsync(
        string userId,
        string username,
        CancellationToken cancellationToken);

    /// <summary>
    /// Blocks the account from logging in again. One-way on purpose: there is no enabled flag to pass
    /// because the app offers no way back -- only an administrator can re-enable the account from
    /// Keycloak. Idempotent, so a retry after a failure is safe.
    /// </summary>
    Task DeactivateParticipantAsync(string userId, CancellationToken cancellationToken);

    Task LogoutParticipantSessionsAsync(string userId, CancellationToken cancellationToken);
}
