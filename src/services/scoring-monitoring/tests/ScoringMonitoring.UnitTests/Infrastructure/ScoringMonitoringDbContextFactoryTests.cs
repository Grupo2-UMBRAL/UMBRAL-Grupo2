using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ScoringMonitoring.Infrastructure.Persistence;
using Xunit;

namespace ScoringMonitoring.UnitTests.Infrastructure;

[Collection("SequentialEnvironment")]
public class ScoringMonitoringDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_WithValidConnectionString_ReturnsConfiguredDbContext()
    {
        // Arrange — the factory reads ConnectionStrings:Postgres via IConfiguration,
        // which maps to the env var ConnectionStrings__Postgres.
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "Host=localhost;Database=umbral_test;Username=postgres;Password=postgres");
        try
        {
            var factory = new ScoringMonitoringDbContextFactory();

            // Act
            var dbContext = factory.CreateDbContext(Array.Empty<string>());

            // Assert
            Assert.NotNull(dbContext);
            Assert.IsType<ScoringMonitoringDbContext>(dbContext);
            Assert.True(dbContext.Database.IsNpgsql(), "DbContext should be configured with Npgsql (PostgreSQL).");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", null);
        }
    }

    [Fact]
    public void CreateDbContext_WithoutConnectionString_ThrowsInvalidOperationException()
    {
        // Arrange — ensure no connection string is available
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "");
        try
        {
            var factory = new ScoringMonitoringDbContextFactory();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext(Array.Empty<string>()));
            Assert.Contains("ConnectionStrings:Postgres", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", null);
        }
    }
}
