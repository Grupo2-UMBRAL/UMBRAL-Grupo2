namespace ScoringMonitoring.Infrastructure.Messaging;

/// <summary>
/// Bound from the "RabbitMQ" configuration section. In docker-compose these come
/// from the RabbitMQ__Host / __Port / __User / __Password environment variables.
/// </summary>
public sealed class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string User { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}
