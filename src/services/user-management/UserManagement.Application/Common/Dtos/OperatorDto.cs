namespace UserManagement.Application.Common.Dtos;

/// <summary>
/// Application-level read model for an operator. Insulates the API/wire contract from the
/// <see cref="UserManagement.Domain.Entities.OperatorUser"/> domain entity so the transport shape
/// can evolve independently of the domain.
/// </summary>
/// <param name="Id" example="7c9e6679-7425-40de-944b-e07fc1f90ae7">Keycloak user id; the value to pass back as <c>userId</c> when deactivating or rotating the password.</param>
/// <param name="Username" example="a.silva">Login handle in the realm. Letters, digits, dot, underscore or dash only.</param>
/// <param name="Email" example="a.silva@umbral.cl">Where credential and password-rotation notices are delivered. Stored lower-cased.</param>
/// <param name="FirstName" example="Ana">Display name shown in the admin console; not used for login.</param>
/// <param name="LastName" example="Silva">Display name shown in the admin console; not used for login.</param>
/// <param name="IsActive">False once the Operator has been deactivated in Keycloak: the account is kept for auditing but can no longer sign in.</param>
public sealed record OperatorDto(
    string Id,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive);
