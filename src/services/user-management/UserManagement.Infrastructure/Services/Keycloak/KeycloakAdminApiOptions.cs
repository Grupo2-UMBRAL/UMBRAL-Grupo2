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
}
