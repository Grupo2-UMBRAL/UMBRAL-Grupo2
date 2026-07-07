namespace UserManagement.Application.Abstractions;

public interface IEmailNotificationService
{
    Task SendOperatorCredentialsAsync(
        string email,
        string username,
        string rawPassword,
        CancellationToken cancellationToken = default);

    Task SendPasswordRotatedAsync(
        string email,
        string username,
        string newPassword,
        CancellationToken cancellationToken = default);
}
