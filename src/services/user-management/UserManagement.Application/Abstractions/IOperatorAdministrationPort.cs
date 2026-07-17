using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Abstractions;

public interface IOperatorAdministrationPort
{
    Task<IReadOnlyList<OperatorDto>> ListOperatorsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<OperatorDto>> FindUsersByEmailAsync(string email, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperatorDto>> FindUsersByUsernameAsync(string username, CancellationToken cancellationToken);
    /// <summary>
    /// Provisions a Keycloak user. A null or blank <paramref name="password"/> creates the account
    /// with no credential and an unverified email — it cannot sign in until an onboarding invitation
    /// is completed. Supplying a password keeps the legacy behaviour (permanent credential, verified
    /// email), which self-registering Participants still rely on. First and last name are optional so
    /// an Operator can fill them in later via the onboarding profile step.
    /// </summary>
    Task<string> CreateUserAsync(string username, string email, string? firstName, string? lastName, string? password, CancellationToken cancellationToken);

    /// <summary>
    /// Emails the Operator a one-time onboarding link (Keycloak <c>execute-actions-email</c>) that walks
    /// them through setting a password, completing their profile, and verifying their email. Idempotent:
    /// calling it again simply issues a fresh link, so it doubles as "resend invitation". The required
    /// actions, issuing client, and redirect are the adapter's concern and are not exposed to callers.
    /// </summary>
    Task SendOperatorOnboardingInvitationAsync(string userId, CancellationToken cancellationToken);
    Task SendOperatorPasswordResetAsync(string userId, CancellationToken cancellationToken);

    Task AssignOperatorRoleAsync(string userId, CancellationToken cancellationToken);
    Task AssignParticipantRoleAsync(string userId, CancellationToken cancellationToken);
    Task DeleteUserAsync(string userId, CancellationToken cancellationToken);
    Task<OperatorDto?> GetUserByIdAsync(string userId, CancellationToken cancellationToken);
    Task<OperatorDto> SetUserEnabledAsync(string userId, bool enabled, CancellationToken cancellationToken);
}
