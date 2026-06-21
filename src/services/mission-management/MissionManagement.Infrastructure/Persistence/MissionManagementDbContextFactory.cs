using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Umbral.ServiceDefaults;

namespace MissionManagement.Infrastructure.Persistence;

public sealed class MissionManagementDbContextFactory : IDesignTimeDbContextFactory<MissionManagementDbContext>
{
    public MissionManagementDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = ServiceConfiguration.GetRequiredPostgresConnectionString(configuration);
        var optionsBuilder = new DbContextOptionsBuilder<MissionManagementDbContext>();

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(MissionManagementDbContext).Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", MissionManagementPersistence.SchemaName);
            });

        return new MissionManagementDbContext(optionsBuilder.Options);
    }
}
