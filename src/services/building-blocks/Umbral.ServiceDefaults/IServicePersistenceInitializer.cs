namespace Umbral.ServiceDefaults;

public interface IServicePersistenceInitializer
{
    Task ApplyPendingMigrationsAsync(CancellationToken cancellationToken);
}
