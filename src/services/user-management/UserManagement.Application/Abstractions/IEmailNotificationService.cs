namespace UserManagement.Application.Abstractions;

public interface IEmailNotificationService
{
    Task SendParticipantWelcomeAsync(
        string email,
        string username,
        CancellationToken cancellationToken = default);
}
