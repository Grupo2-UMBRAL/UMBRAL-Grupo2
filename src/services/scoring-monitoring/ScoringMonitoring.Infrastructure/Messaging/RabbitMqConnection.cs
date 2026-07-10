using RabbitMQ.Client;

namespace ScoringMonitoring.Infrastructure.Messaging;

/// <summary>
/// Owns a single long-lived RabbitMQ connection for the process. Connections are
/// expensive and meant to be shared; channels are cheap and NOT thread-safe.
/// </summary>
public sealed class RabbitMqConnection(RabbitMqOptions options) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IConnection? connection;

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (connection is { IsOpen: true })
        {
            return connection;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (connection is { IsOpen: true })
            {
                return connection;
            }

            var factory = new ConnectionFactory
            {
                HostName = options.Host,
                Port = options.Port,
                UserName = options.User,
                Password = options.Password,
            };

            connection = await factory.CreateConnectionAsync(cancellationToken);
            return connection;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync();
        }

        gate.Dispose();
    }
}
