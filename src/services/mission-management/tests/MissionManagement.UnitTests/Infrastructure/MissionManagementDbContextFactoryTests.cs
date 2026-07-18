using System;
using System.Threading.Tasks;
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
        // Arrange
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "Host=localhost;Database=umbral_test;Username=postgres;Password=postgres");
        try
        {
            var factory = new MissionManagementDbContextFactory();

            // Act
            var dbContext = factory.CreateDbContext(Array.Empty<string>());

            // Assert
            Assert.NotNull(dbContext);
            Assert.IsType<MissionManagementDbContext>(dbContext);
            Assert.True(dbContext.Database.IsNpgsql(), "dbContext should be configured with Npgsql (PostgreSQL).");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", null);
        }
    }

    [Fact]
    public void CreateDbContext_WithoutConnectionString_ThrowsInvalidOperationException()
    {
        // Arrange
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "");
        try
        {
            var factory = new MissionManagementDbContextFactory();

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
