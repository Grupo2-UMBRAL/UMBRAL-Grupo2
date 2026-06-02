namespace Umbral.ServiceDefaults;

public static class UmbralRequestAuthorizations
{
    public static RequestAuthorizationMetadata AuthenticatedOnly { get; } = new([]);

    public static RequestAuthorizationMetadata AdministratorOnly { get; } =
        new([UmbralRoles.Administrator]);

    public static RequestAuthorizationMetadata OperatorOnly { get; } =
        new([UmbralRoles.Operator]);

    public static RequestAuthorizationMetadata ParticipantOnly { get; } =
        new([UmbralRoles.Participant]);

    public static RequestAuthorizationMetadata AdministratorOrOperator { get; } =
        new([UmbralRoles.Administrator, UmbralRoles.Operator]);
}
