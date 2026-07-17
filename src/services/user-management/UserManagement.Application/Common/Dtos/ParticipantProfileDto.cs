namespace UserManagement.Application.Common.Dtos;

/// <summary>
/// The authenticated Participant's own account, as shown by the mobile profile screen. Distinct from
/// <see cref="ParticipantDto"/>, which answers registration and deliberately omits the email.
/// </summary>
/// <param name="UserId" example="7c9e6679-7425-40de-944b-e07fc1f90ae7">Keycloak user id, always the `sub` of the caller's own token.</param>
/// <param name="Username" example="pao.rojas">The handle as stored in the realm; also what the Participant logs in with.</param>
/// <param name="Email" example="pao.rojas@example.cl">The Participant's own email. Returned on purpose: this is a self-scoped endpoint, and the password flow hands the player over to Keycloak's Account Console, where they need to know which account they are signing in as.</param>
/// <param name="IsActive" example="true">Whether the account can still log in. Turns false once the Participant deactivates it, and only an administrator can turn it back.</param>
public sealed record ParticipantProfileDto(
    string UserId,
    string Username,
    string Email,
    bool IsActive);
