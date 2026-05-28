using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Infrastructure;

public sealed class MissionDesignDbContextFactory : IDesignTimeDbContextFactory<MissionDesignDbContext>
{
    public MissionDesignDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = ServiceConfiguration.GetRequiredPostgresConnectionString(configuration);
        var optionsBuilder = new DbContextOptionsBuilder<MissionDesignDbContext>();

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(MissionDesignDbContext).Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", MissionDesignPersistence.SchemaName);
            });

        return new MissionDesignDbContext(optionsBuilder.Options);
    }
}
