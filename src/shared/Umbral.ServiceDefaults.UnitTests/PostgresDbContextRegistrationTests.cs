using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Umbral.ServiceDefaults.UnitTests;

public sealed class PostgresDbContextRegistrationTests
{
    private const string ConnectionString =
        "Host=localhost;Database=umbral;Username=umbral;Password=umbral";

    [Fact]
    public void AddUmbralPostgresDbContext_ResolvesContext_FromConfiguredConnectionString()
    {
        using var provider = BuildProvider(ConnectionString);

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        Assert.NotNull(context);
    }

    [Fact]
    public void AddUmbralPostgresDbContext_RegistersContextAsScoped()
    {
        using var provider = BuildProvider(ConnectionString);

        TestDbContext first;
        TestDbContext second;
        using (var scope = provider.CreateScope())
        {
            first = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            second = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        }

        TestDbContext otherScope;
        using (var scope = provider.CreateScope())
        {
            otherScope = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        }

        // Same scope -> same instance; different scope -> different instance.
        Assert.Same(first, second);
        Assert.NotSame(first, otherScope);
    }

    [Fact]
    public void AddUmbralPostgresDbContext_Throws_WhenConnectionStringIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddUmbralPostgresDbContext<TestDbContext>(configuration, "umbral_tests"));

        Assert.Equal(
            "Missing required connection string 'ConnectionStrings:Postgres'.",
            exception.Message);
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = connectionString
            })
            .Build();

        services.AddUmbralPostgresDbContext<TestDbContext>(configuration, "umbral_tests");

        return services.BuildServiceProvider();
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);
}
