using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Xunit;

namespace Umbral.ServiceDefaults.UnitTests;

public sealed class TelemetryRegistrationTests
{
    [Fact]
    public void AddUmbralTelemetry_WritesConsoleLogsAsJson()
    {
        var services = new ServiceCollection();
        services.AddUmbralTelemetry("umbral-tests");
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<ConsoleLoggerOptions>>().CurrentValue;

        Assert.Equal(ConsoleFormatterNames.Json, options.FormatterName);
    }
}
