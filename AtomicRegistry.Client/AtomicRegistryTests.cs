using System.Collections.Concurrent;
using AtomicRegistry.Configuration;
using AtomicRegistry.Dto;
using FluentAssertions;
using NUnit.Framework;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.Logging.File;
using Vostok.Logging.File.Configuration;

namespace AtomicRegistry.Client;

public class AtomicRegistryTests
{
    private AtomicRegistryClient client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        client = CreateClient("default");
    }

    //todo: test with three versions of value
    [SetUp]
    public async Task SetUp()
    {
        foreach (var replica in AtomicRegistryNodeClusterProvider.InstancesTopology().Keys)
            await client.ResetFault(replica);
        await client.Drop(); //todo: research cool ways for atomic tests and not wait 5 seconds
    }

    [Test]
    public async Task TestSetGet()
    {
        var value = $"test-yeah-{Guid.NewGuid()}";
        await client.Set(value);
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
        var freezeSettings = FaultSettingsDto.AllFrozen;
        await client.InduceFault("Instance1", freezeSettings);

        var value = $"test-yeah-{Guid.NewGuid()}";
        await client.Set(value);
        var result = await client.Get();
        result.Should().Be(value);
    }

    [Test]
    public async Task TwoReplicasFrozen_ClusterIsDead_OneReplicaUp_ClusterAlive()
    {
        var freezeSettings = FaultSettingsDto.AllFrozen;
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

    [Test]
    public async Task ReplicaFrozen_RequestsPileUp_ReplicaUnfrozen_ReturnLatest()
    {
        var value1 = $"value1-{Guid.NewGuid()}";
        var value2 = $"value2-{Guid.NewGuid()}";
        var value3 = $"value3-{Guid.NewGuid()}";
        var value4 = $"value4-{Guid.NewGuid()}";

        var freezeSettings = FaultSettingsDto.AllFrozen;
        await client.InduceFault("Instance1", freezeSettings);

        await client.Set(value1);
        await client.Set(value2);
        await client.Set(value3);
        await client.Set(value4);

        var result1 = await client.Get();
        result1.Should().Be(value4);

        await client.ResetFault("Instance1");

        var result2 = await client.Get();
        result2.Should().Be(value4);
    }

    [Test]
    public async Task MultipleReads_ReceiveSameResults()
    {
        var value1 = $"value1-{Guid.NewGuid()}";
        await client.Set(value1);
        for (var i = 0; i < 100; i++)
        {
            var result1 = await client.Get();
            result1.Should().Be(value1);
        }
    }

    [Test]
    public async Task ParallelReads_ReceiveSameResults()
    {
        var value1 = $"value1-{Guid.NewGuid()}";
        await client.Set(value1);

        Action[] tasks = Enumerable.Repeat(() =>
        {
            var result1 = client.Get().GetAwaiter().GetResult();
            result1.Should().Be(value1);
        }, 50).ToArray();

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 10 };

        Parallel.Invoke(parallelOptions, tasks);
    }

    [Test]
    public async Task SetIsTakingTooLong_GetRequestsHelpItRestore()
    {
        var value1 = $"value1-{Guid.NewGuid()}";

        Console.WriteLine($"This test value is {value1}");

        await client.InduceFault("Instance1", FaultSettingsDto.OneSetFrozen);
        await client.InduceFault("Instance3", FaultSettingsDto.OneSetFrozen);

        var setTask1 = Task.Run(() => client.Set(value1));
        Thread.Sleep(TimeSpan.FromSeconds(1));
        setTask1.IsCompleted.Should().BeFalse();

        string? currentGetResult = null;
        while (currentGetResult != value1) currentGetResult = (await client.Get())?.Value;


        Action[] tasks = Enumerable.Repeat(() =>
        {
            var result1 = client.Get().GetAwaiter().GetResult();
            result1.Should().Be(value1);
        }, 100).ToArray();

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 20 };
        Parallel.Invoke(parallelOptions, tasks);

        await client.InduceFault("Instance1", FaultSettingsDto.UnfreezeFrozenSets);
        await client.InduceFault("Instance3", FaultSettingsDto.UnfreezeFrozenSets);

        await setTask1;

        var result2 = await client.Get();
        result2.Should().Be(value1);
    }

    [Test]
    public async Task TwoWriters_GetSetIsWorking()
    {
        var client1Value = $"client1-value1-{Guid.NewGuid()}";
        var client2Value = $"client2-value1-{Guid.NewGuid()}";

        var client1 = CreateClient("client1");
        var client2 = CreateClient("client2");

        await client1.Set(client1Value);
        await client2.Set(client2Value);

        var result1 = await client1.Get();
        var result2 = await client2.Get();

        result1.Should().Be(client2Value);
        result2.Should().Be(client2Value);
    }

    [Test]
    public async Task TwoWriters_OneWriteIsLaggingAndRejectedAfterSecond()
    {
        var client1Value = $"client1-value1-{Guid.NewGuid()}";
        var client2Value = $"client2-value1-{Guid.NewGuid()}";

        var client1 = CreateClient("client1");
        var client2 = CreateClient("client2");

        await client.InduceFault("Instance1", FaultSettingsDto.OneSetFrozen);

        await client1.Set(client1Value);

        await client.InduceFault("Instance3", FaultSettingsDto.OneSetFrozen);

        await client2.Set(client2Value);

        var result1 = await client1.Get();
        var result2 = await client2.Get();

        result1.Should().Be(client2Value);
        result2.Should().Be(client2Value);
    }

    [Test]
    public async Task ManyWritersTest()
    {
        ThreadPoolUtility.SetUp(1024);

        var lastResults = new ConcurrentStack<string>();
        var clientsCount = 100;
        var requestsPerClientCount = 4;

        var clients = Enumerable
            .Range(0, clientsCount)
            .Select(x => CreateClient(x.ToString(), shouldNotUseLogs: true))
            .ToArray();


        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 10 };

        async Task DoSet(AtomicRegistryClient c)
        {
            var random = new Random();
            var value = Guid.NewGuid().ToString();
            await c.Set(value);
            await Task.Delay(random.Next(0, 3));
            value = Guid.NewGuid().ToString();
            await c.Set(value);
            await Task.Delay(random.Next(0, 10));
            value = Guid.NewGuid().ToString();
            await c.Set(value);
            await Task.Delay(random.Next(0, 20));
            value = Guid.NewGuid().ToString();
            await c.Set(value);
            lastResults.Push(value);
        }

        await Parallel.ForEachAsync(clients, parallelOptions, async (c, _) => { await DoSet(c); });

        var result = await client.Get();
        (result?.Timestamp + 1).Should().BeGreaterThan(clientsCount * requestsPerClientCount);
        result?.Value.Should().BeOneOf(lastResults.Take(10));
    }

    //todo: test about many writers and monotonous ts growth
    private static AtomicRegistryClient CreateClient(string clientId, bool shouldNotUseLogs = false)
    {
        var consoleLog = new ConsoleLog();
        var fileLogSettings = new FileLogSettings { FilePath = $"LocalRuns\\test-client-log-{clientId}.txt" };
        var fileLog = new FileLog(fileLogSettings);
        ILog resultLog = shouldNotUseLogs ? new SilentLog() : new CompositeLog(consoleLog, fileLog);
        return new AtomicRegistryClient(new AtomicRegistryNodeClusterProvider(),
            resultLog, clientId);
    }
}