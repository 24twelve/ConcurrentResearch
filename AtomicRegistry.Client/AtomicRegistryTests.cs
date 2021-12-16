using AtomicRegistry.Dto;
using FluentAssertions;
using Microsoft.JSInterop;
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
        public async Task SetUp()
        {
            var consoleLog = new ConsoleLog();
            var fileLogSettings = new FileLogSettings { FilePath = "LocalRuns\\test-log.txt" };
            var fileLog = new FileLog(fileLogSettings);
            client = new AtomicRegistryClient(new AtomicRegistryNodeClusterProvider(),
                new CompositeLog(consoleLog, fileLog));
            foreach (var replica in AtomicRegistryNodeClusterProvider.InstancesTopology().Keys)
                await client.ResetFault(replica);
            await client.Drop();
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
            await client.Set(value1);
            await client.Set(value2);
            await client.Set(value3);
            var result = await client.Get();
            result.Should().Be(value3);
        }

        [Test]
        public async Task InduceFreeze_ReplicaFrozen_ClusterIsWorking()
        {
            var freezeSettings = FaultSettingsDto.Frozen;
            await client.InduceFault("Instance1", freezeSettings);

            var value = $"test-yeah-{Guid.NewGuid()}";
            await client.Set(value);
            var result = await client.Get();
            result.Should().Be(value);
        }

        [Test]
        public async Task InduceDown_ReplicaDown_ClusterIsWorking()
        {
            var freezeSettings = FaultSettingsDto.Down;
            await client.InduceFault("Instance1", freezeSettings);

            var value = $"test-yeah-{Guid.NewGuid()}";
            await client.Set(value);
            var result = await client.Get();
            result.Should().Be(value);
        }

        [Test]
        public async Task TwoReplicasDown_ClusterIsDead_OneReplicaUp_ClusterAlive()
        {
            var freezeSettings = FaultSettingsDto.Down;
            await client.InduceFault("Instance1", freezeSettings);
            await client.InduceFault("Instance2", freezeSettings);

            var value = $"test-yeah-{Guid.NewGuid()}";

            var task = Task.Run(() => client.Set(value));

            Thread.Sleep(TimeSpan.FromSeconds(1));

            task.IsCompleted.Should().BeFalse();

            await client.ResetFault("Instance1");

            await task;
            (await client.Get()).Should().Be(value);
        }
    }
}