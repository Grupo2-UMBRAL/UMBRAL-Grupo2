using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Umbral.ServiceDefaults;

namespace ScoringAudit.Infrastructure.Persistence;

public sealed class ScoringAuditDbContextFactory : IDesignTimeDbContextFactory<ScoringAuditDbContext>
{
    public ScoringAuditDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = ServiceConfiguration.GetRequiredPostgresConnectionString(configuration);
        var optionsBuilder = new DbContextOptionsBuilder<ScoringAuditDbContext>();

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(ScoringAuditDbContext).Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", ScoringAuditPersistence.SchemaName);
            });

        return new ScoringAuditDbContext(optionsBuilder.Options);
    }
}
