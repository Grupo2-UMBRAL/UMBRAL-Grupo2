using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MissionManagement.Infrastructure.Persistence;
using Xunit;

namespace MissionManagement.UnitTests.Infrastructure;

[Collection("SequentialEnvironment")]
public class MissionManagementDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_WithValidConnectionString_ReturnsConfiguredDbContext()
    {
        // Arrange — the factory reads ConnectionStrings:Postgres via IConfiguration,
        // which maps to the env var ConnectionStrings__Postgres.
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "Host=localhost;Database=umbral_test;Username=postgres;Password=postgres");
        try
        {
            var factory = new MissionManagementDbContextFactory();

            // Act
            var dbContext = factory.CreateDbContext(Array.Empty<string>());

            // Assert
            Assert.NotNull(dbContext);
            Assert.IsType<MissionManagementDbContext>(dbContext);
            Assert.True(dbContext.Database.IsNpgsql(), "DbContext should be configured with Npgsql (PostgreSQL).");
        }
        finally
        {
            // Cleanup — remove env var so it doesn't pollute other tests
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
            var factory = new MissionManagementDbContextFactory();

            // Act & Assert — the factory delegates to ServiceConfiguration.GetRequiredPostgresConnectionString
            // which throws InvalidOperationException when the connection string is missing.
            var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext(Array.Empty<string>()));
            Assert.Contains("ConnectionStrings:Postgres", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", null);
        }
    }
}
