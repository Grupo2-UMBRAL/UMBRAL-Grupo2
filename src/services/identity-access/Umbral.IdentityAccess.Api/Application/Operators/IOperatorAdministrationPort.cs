namespace Umbral.IdentityAccess.Api.Application.Operators;

public interface IOperatorAdministrationPort
{
    Task<IReadOnlyList<OperatorUser>> ListOperatorsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<OperatorUser>> FindUsersByEmailAsync(string email, CancellationToken cancellationToken);

    Task<IReadOnlyList<OperatorUser>> FindUsersByUsernameAsync(string username, CancellationToken cancellationToken);

    Task<CreatedUserReference> CreateUserAsync(
        ValidatedCreateOperatorInput input,
        CancellationToken cancellationToken);

    Task AssignOperatorRoleAsync(string userId, CancellationToken cancellationToken);

    Task DeleteUserAsync(string userId, CancellationToken cancellationToken);

    Task<OperatorUser?> GetUserByIdAsync(string userId, CancellationToken cancellationToken);

    Task<OperatorUser> SetUserEnabledAsync(string userId, bool enabled, CancellationToken cancellationToken);

    Task RotateOperatorPasswordAsync(string userId, string password, CancellationToken cancellationToken);
}
