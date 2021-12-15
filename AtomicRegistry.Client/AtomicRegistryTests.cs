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
        private AtomicRegistryClient client = null!;

        [SetUp]
        public void SetUp()
        {
            var consoleLog = new ConsoleLog();
            var fileLogSettings = new FileLogSettings { FilePath = "LocalRuns\\test-log.txt" };
            var fileLog = new FileLog(fileLogSettings);
            client = new AtomicRegistryClient(new AtomicRegistryNodeClusterProvider(),
                new CompositeLog(consoleLog, fileLog));
        }

        [Test]
        public async Task TestSetGet()
        {
            var value = $"test-yeah-{Guid.NewGuid()}";
            client.Set(value);
            var result = await client.Get();
            result.Should().Be(value);
        }

        [Test]
        public async Task TestSeveralSets()
        {
            var value1 = $"test-yeah-{Guid.NewGuid()}";
            var value2 = $"test-yeah-{Guid.NewGuid()}";
            var value3 = $"test-yeah-{Guid.NewGuid()}";
            client.Set(value1);
            client.Set(value2);
            client.Set(value3);
            var result = await client.Get();
            result.Should().Be(value3);
        }
    }
}