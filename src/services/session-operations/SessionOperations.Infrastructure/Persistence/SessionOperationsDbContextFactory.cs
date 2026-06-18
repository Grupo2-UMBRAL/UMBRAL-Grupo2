using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Umbral.ServiceDefaults;

namespace SessionOperations.Infrastructure.Persistence;

public sealed class SessionOperationsDbContextFactory : IDesignTimeDbContextFactory<SessionOperationsDbContext>
{
    public SessionOperationsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = ServiceConfiguration.GetRequiredPostgresConnectionString(configuration);
        var optionsBuilder = new DbContextOptionsBuilder<SessionOperationsDbContext>();

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(SessionOperationsDbContext).Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", SessionOperationsPersistence.SchemaName);
            });

        return new SessionOperationsDbContext(optionsBuilder.Options);
    }
}
