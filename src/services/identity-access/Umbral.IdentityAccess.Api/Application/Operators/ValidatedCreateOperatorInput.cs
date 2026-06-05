namespace Umbral.IdentityAccess.Api.Application.Operators;

public sealed record ValidatedCreateOperatorInput(
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string Password);
