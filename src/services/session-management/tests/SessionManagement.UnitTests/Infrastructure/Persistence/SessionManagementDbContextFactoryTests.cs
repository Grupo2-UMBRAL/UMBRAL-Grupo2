using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using SessionManagement.Infrastructure.Persistence;
using Xunit;

namespace SessionManagement.UnitTests.Infrastructure.Persistence;

public class SessionManagementDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_ShouldReturnConfiguredDbContext()
    {
        // Arrange
        var factory = new SessionManagementDbContextFactory();

        // Ensure a dummy appsettings.json or environment variable exists so it doesn't fail on connection string.
        // We will mock the environment variable instead of writing a file, as the ConfigurationBuilder reads from EnvironmentVariables.
        var connectionStringEnvVar = "ConnectionStrings__UmbralDb";
        var originalEnvVar = Environment.GetEnvironmentVariable(connectionStringEnvVar);
        
        try
        {
            Environment.SetEnvironmentVariable(connectionStringEnvVar, "Host=localhost;Database=umbral_test;Username=postgres;Password=postgres");

            // Act
            var dbContext = factory.CreateDbContext(Array.Empty<string>());

            // Assert
            Assert.NotNull(dbContext);
            
            // Check that it's configured for PostgreSQL
            Assert.True(dbContext.Database.IsNpgsql());
        }
        finally
        {
            // Restore original
            Environment.SetEnvironmentVariable(connectionStringEnvVar, originalEnvVar);
        }
    }
}
