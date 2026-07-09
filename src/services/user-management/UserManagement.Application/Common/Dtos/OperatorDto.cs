namespace UserManagement.Application.Common.Dtos;

/// <summary>
/// Application-level read model for an operator. Insulates the API/wire contract from the
/// <see cref="UserManagement.Domain.Entities.OperatorUser"/> domain entity so the transport shape
/// can evolve independently of the domain.
/// </summary>
public sealed record OperatorDto(
    string Id,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive);
