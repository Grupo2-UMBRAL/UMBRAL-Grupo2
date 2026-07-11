using UserManagement.Application.Common.Dtos;

namespace UserManagement.Application.Abstractions;

public interface IOperatorAdministrationPort
{
    Task<IReadOnlyList<OperatorDto>> ListOperatorsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<OperatorDto>> FindUsersByEmailAsync(string email, CancellationToken cancellationToken);
    Task<IReadOnlyList<OperatorDto>> FindUsersByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<string> CreateUserAsync(string username, string email, string firstName, string lastName, string password, CancellationToken cancellationToken);
    Task AssignOperatorRoleAsync(string userId, CancellationToken cancellationToken);
    Task AssignParticipantRoleAsync(string userId, CancellationToken cancellationToken);
    Task DeleteUserAsync(string userId, CancellationToken cancellationToken);
    Task<OperatorDto?> GetUserByIdAsync(string userId, CancellationToken cancellationToken);
    Task<OperatorDto> SetUserEnabledAsync(string userId, bool enabled, CancellationToken cancellationToken);
    Task RotateOperatorPasswordAsync(string userId, string password, CancellationToken cancellationToken);
}
