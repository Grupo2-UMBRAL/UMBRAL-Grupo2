using UserManagement.Domain.Entities;

namespace UserManagement.Application.Abstractions;

public interface IOperatorAdministrationPort
{
    Task<IReadOnlyList<OperatorUser>> ListOperatorsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<OperatorUser>> FindUsersByEmailAsync(string email, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperatorUser>> FindUsersByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<CreatedUserReference> CreateUserAsync(string username, string email, string firstName, string lastName, string password, CancellationToken cancellationToken);
    Task AssignOperatorRoleAsync(string userId, CancellationToken cancellationToken);
    Task AssignParticipantRoleAsync(string userId, CancellationToken cancellationToken);
    Task DeleteUserAsync(string userId, CancellationToken cancellationToken);
    Task<OperatorUser?> GetUserByIdAsync(string userId, CancellationToken cancellationToken);
    Task<OperatorUser> SetUserEnabledAsync(string userId, bool enabled, CancellationToken cancellationToken);
    Task RotateOperatorPasswordAsync(string userId, string password, CancellationToken cancellationToken);
}
