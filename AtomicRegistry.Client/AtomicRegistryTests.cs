using FluentAssertions;
using NUnit.Framework;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.Logging.File;
using Vostok.Logging.File.Configuration;

namespace AtomicRegistry.Client
{
    public class AtomicRegistryTests
    {
        [Test]
        public async Task TestSetGet()
        {
            var consoleLog = new ConsoleLog();
            var fileLogSettings = new FileLogSettings { FilePath = "LocalRuns\\test-log.txt"};
            var fileLog = new FileLog(fileLogSettings);
            var client = new AtomicRegistryClient(new AtomicRegistryNodeClusterProvider(),
                new CompositeLog(consoleLog, fileLog));
            client.Set("test-yeah");
            var result = await client.Get();
            result.Should().Be("test-yeah");
        }
    }
}