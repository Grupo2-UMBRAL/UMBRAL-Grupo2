namespace UserManagement.Infrastructure.Services.Email;

public sealed class SmtpEmailOptions
{
    public const string SectionName = "Email:Smtp";

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1025;
    public string FromAddress { get; init; } = "no-reply@umbral.app";
    public string FromName { get; init; } = "UMBRAL";
    public bool UseSsl { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
}
