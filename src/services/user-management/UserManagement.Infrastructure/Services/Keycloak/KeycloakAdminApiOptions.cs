using Umbral.ServiceDefaults;

namespace UserManagement.Infrastructure.Keycloak;

public sealed class KeycloakAdminApiOptions
{
    public const string SectionName = "UserManagement:Keycloak";

    public string BaseUrl { get; init; } = "http://keycloak:8080";

    public string Realm { get; init; } = "umbral";

    public string AdminRealm { get; init; } = "master";

    public string AdminClientId { get; init; } = "admin-cli";

    public string AdminUsername { get; init; } = string.Empty;

    public string AdminPassword { get; init; } = string.Empty;

    public string OperatorRoleName { get; init; } = UmbralRoles.Operator;

    public string ParticipantRoleName { get; init; } = UmbralRoles.Participant;

    // Onboarding invitation (execute-actions-email). The Operator receives a one-time link from
    // Keycloak instead of a password from the Administrator, so these describe where that link lands.

    /// <summary>Public client the onboarding link is issued for; must whitelist <see cref="OnboardingRedirectUri"/>.</summary>
    public string WebClientId { get; init; } = "umbral-web";

    /// <summary>
    /// Where Keycloak returns the Operator after they finish the required actions. Left blank the link
    /// falls back to Keycloak's own account console; set it to a whitelisted <see cref="WebClientId"/>
    /// redirect so onboarding ends inside UMBRAL.
    /// </summary>
    public string OnboardingRedirectUri { get; init; } = string.Empty;

    /// <summary>Lifetime of the onboarding link in seconds. Zero or less keeps Keycloak's realm default (12h).</summary>
    public int OnboardingLinkLifespanSeconds { get; init; }
}
