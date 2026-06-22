using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Umbral.ServiceDefaults;

namespace ScoringMonitoring.Infrastructure.Persistence;

public sealed class ScoringMonitoringDbContextFactory : IDesignTimeDbContextFactory<ScoringMonitoringDbContext>
{
    public ScoringMonitoringDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = ServiceConfiguration.GetRequiredPostgresConnectionString(configuration);
        var optionsBuilder = new DbContextOptionsBuilder<ScoringMonitoringDbContext>();

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(ScoringMonitoringDbContext).Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", ScoringMonitoringPersistence.SchemaName);
            });

        return new ScoringMonitoringDbContext(optionsBuilder.Options);
    }
}
