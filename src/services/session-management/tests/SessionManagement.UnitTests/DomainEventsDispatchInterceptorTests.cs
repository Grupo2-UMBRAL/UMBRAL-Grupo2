using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SessionManagement.Domain.Abstractions;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Infrastructure.Persistence;
using Umbral.Contracts.Audit;
using Xunit;

namespace SessionManagement.UnitTests;

public class TestAggregate : AggregateRoot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public void DoSomethingThatRaisesAnEvent()
    {
        var evt = new EvidenceSubmittedDomainEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "trivia",
            DateTimeOffset.UtcNow);

        RaiseDomainEvent(evt);
    }
}

public class TestDbContext : DbContext
{
    public DbSet<TestAggregate> TestAggregates { get; set; } = null!;

    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestAggregate>().HasKey(x => x.Id);
    }
}

public class DomainEventsDispatchInterceptorTests
{
    [Fact]
    public async Task SavingChangesAsync_WithDomainEvents_DispatchesMappedEventsAndClearsThem()
    {
        // Arrange
        var mockPublishEndpoint = new Mock<IPublishEndpoint>();
        
        var services = new ServiceCollection();
        services.AddSingleton(mockPublishEndpoint.Object);
        var serviceProvider = services.BuildServiceProvider();

        var interceptor = new DomainEventsDispatchInterceptor(serviceProvider);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new TestDbContext(options);
        
        var aggregate = new TestAggregate();
        aggregate.DoSomethingThatRaisesAnEvent();

        context.TestAggregates.Add(aggregate);

        // Act
        await context.SaveChangesAsync();

        // Assert
        Assert.Empty(aggregate.DomainEvents);
        mockPublishEndpoint.Verify(
            x => x.Publish(It.IsAny<SessionAuditEventMessage>(), It.IsAny<CancellationToken>()), 
            Times.Once);
    }
    
    [Fact]
    public async Task SavingChangesAsync_WithoutDomainEvents_DoesNotPublish()
    {
        // Arrange
        var mockPublishEndpoint = new Mock<IPublishEndpoint>();
        
        var services = new ServiceCollection();
        services.AddSingleton(mockPublishEndpoint.Object);
        var serviceProvider = services.BuildServiceProvider();

        var interceptor = new DomainEventsDispatchInterceptor(serviceProvider);

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        await using var context = new TestDbContext(options);
        
        var aggregate = new TestAggregate(); // No events raised

        context.TestAggregates.Add(aggregate);

        // Act
        await context.SaveChangesAsync();

        // Assert
        mockPublishEndpoint.Verify(
            x => x.Publish(It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()), 
            Times.Never);
    }
}
