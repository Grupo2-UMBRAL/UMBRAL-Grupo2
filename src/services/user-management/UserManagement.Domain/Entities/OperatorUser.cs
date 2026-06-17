namespace UserManagement.Domain.Entities;

public sealed record OperatorUser(
    string Id,
    string Username,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive);
