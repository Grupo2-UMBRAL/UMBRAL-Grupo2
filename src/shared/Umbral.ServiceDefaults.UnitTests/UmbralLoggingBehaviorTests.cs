using Microsoft.Extensions.Logging;
using Umbral.ServiceDefaults;
using Xunit;

namespace Umbral.ServiceDefaults.UnitTests;

public sealed class UmbralLoggingBehaviorTests
{
    private sealed record LogEntry(LogLevel Level, Exception? Exception);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, exception));
    }

    private sealed class SampleRequest;

    [Fact]
    public async Task Handle_Success_LogsInformation_AndReturnsResponse()
    {
        var logger = new CapturingLogger<UmbralLoggingBehavior<SampleRequest, string>>();
        var behavior = new UmbralLoggingBehavior<SampleRequest, string>(logger);

        var response = await behavior.Handle(new SampleRequest(), () => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", response);
        Assert.All(logger.Entries, entry => Assert.Equal(LogLevel.Information, entry.Level));
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Handle_UmbralServiceException_LogsWarning_AndRethrows()
    {
        var logger = new CapturingLogger<UmbralLoggingBehavior<SampleRequest, string>>();
        var behavior = new UmbralLoggingBehavior<SampleRequest, string>(logger);
        var failure = new UmbralDomainException("sample_invalid", "Invalid.", UmbralFailureCategory.Validation);

        var thrown = await Assert.ThrowsAsync<UmbralDomainException>(
            () => behavior.Handle(new SampleRequest(), () => Task.FromException<string>(failure), CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning && ReferenceEquals(entry.Exception, failure));
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Handle_UnhandledException_LogsError_AndRethrows()
    {
        var logger = new CapturingLogger<UmbralLoggingBehavior<SampleRequest, string>>();
        var behavior = new UmbralLoggingBehavior<SampleRequest, string>(logger);
        var failure = new InvalidOperationException("boom");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => behavior.Handle(new SampleRequest(), () => Task.FromException<string>(failure), CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && ReferenceEquals(entry.Exception, failure));
    }
}
