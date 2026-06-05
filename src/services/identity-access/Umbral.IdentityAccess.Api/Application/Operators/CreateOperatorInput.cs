namespace Umbral.IdentityAccess.Api.Application.Operators;

public sealed record CreateOperatorInput(
    string? Username,
    string? Email,
    string? FirstName,
    string? LastName,
    string? Password);
