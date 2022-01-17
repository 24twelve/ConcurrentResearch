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
        result!.Value.Should().Be(value);
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
        result!.Value.Should().Be(value3);
    }

    [Test]
    public async Task InduceFreeze_ReplicaFrozen_ClusterIsWorking()
    {
        var freezeSettings = FaultSettingsDto.AllFrozen;
        await client.InduceFault("Instance1", freezeSettings);

        var value = $"test-yeah-{Guid.NewGuid()}";
        await client.Set(value);
        var result = await client.Get();
        result!.Value.Should().Be(value);
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
        (await client.Get())!.Value.Should().Be(value);
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
        result1!.Value.Should().Be(value4);

        await client.ResetFault("Instance1");

        var result2 = await client.Get();
        result2!.Value.Should().Be(value4);
    }

    [Test]
    public async Task MultipleReads_ReceiveSameResults()
    {
        var value1 = $"value1-{Guid.NewGuid()}";
        await client.Set(value1);
        for (var i = 0; i < 100; i++)
        {
            var result1 = await client.Get();
            result1!.Value.Should().Be(value1);
        }
    }

    [Test]
    public async Task MultipleParallelReads_ReceiveSameResults()
    {
        ThreadPoolUtility.SetUp(1024);

        var value1 = $"value1-{Guid.NewGuid()}";
        await client.Set(value1);

        var clients = Enumerable.Range(0, 1000).Select(i => CreateClient($"{i}")).ToArray();

        async Task GetAndAssert(AtomicRegistryClient cl)
        {
            var result1 = await cl.Get();
            result1!.Value.Should().Be(value1);
        }

        await Parallel.ForEachAsync(clients, new ParallelOptions { MaxDegreeOfParallelism = 30 },
            async (c, _) => await GetAndAssert(c));
    }

    [TestCase(10, 100)]
    [TestCase(100, 10)]
    public async Task MonotonicReads(int writerCount, int readerCount)
    {
        ThreadPoolUtility.SetUp(1024);
        var cancellationToken = new CancellationTokenSource();

        async Task ThreadPoolStateReporter(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await File.AppendAllLinesAsync("C:\\workspace\\temp\\thread-pool.json",
                    new[] { ThreadPoolUtility.GetThreadPoolState().ToString() });
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        var loggingTask = Task.Run(async () => await ThreadPoolStateReporter(cancellationToken.Token),
            cancellationToken.Token);

        async Task AssertMonotonicReads(AtomicRegistryClient cl, CancellationToken ct)
        {
            var result = await cl.Get();
            result.Should().NotBeNull();
            while (!ct.IsCancellationRequested)
            {
                var result2 = await cl.Get();
                result2!.Timestamp.Should().BeGreaterOrEqualTo(result!.Timestamp!.Value);
                result = result2;
            }
        }

        async Task WriteIndefinitely(AtomicRegistryClient cl, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested) await cl.Set($"{cl.ClientId}-{Guid.NewGuid()}");
        }

        var readerClients = Enumerable.Range(0, readerCount).Select(i => CreateClient($"reader-{i}")).ToArray();
        var writerClients = Enumerable.Range(0, writerCount).Select(i => CreateClient($"writer-{i}")).ToArray();


        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 1024 };

        var writersTask = Task.Run(
            async () => await Parallel.ForEachAsync(writerClients, parallelOptions,
                async (cl, _) => await WriteIndefinitely(cl, cancellationToken.Token)));


        var readersTask = Task.Run(
            async () => await Parallel.ForEachAsync(readerClients, parallelOptions,
                async (cl, _) => await AssertMonotonicReads(cl, cancellationToken.Token)));


        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken.Token);

        cancellationToken.Cancel();
        await writersTask;
        await readersTask;
        await loggingTask;
    }

    [TestCase(10, 100)]
    [TestCase(100, 10)]
    public async Task MonotonicWrites(int writerCount, int readerCount)
    {
        ThreadPoolUtility.SetUp(1024);
        var cancellationToken = new CancellationTokenSource();

        async Task ThreadPoolStateReporter(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await File.AppendAllLinesAsync("C:\\workspace\\temp\\thread-pool.json",
                    new[] { ThreadPoolUtility.GetThreadPoolState().ToString() });
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        var loggingTask = Task.Run(async () => await ThreadPoolStateReporter(cancellationToken.Token),
            cancellationToken.Token);

        var readerClients = Enumerable.Range(0, readerCount).Select(i => CreateClient($"reader-{i}")).ToArray();
        var writerClients = Enumerable.Range(0, writerCount).Select(i => CreateClient($"writer-{i}")).ToArray();
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 1024 };

        async Task AssertMonotonicWrites(AtomicRegistryClient cl, CancellationToken ct)
        {
            var observedVersions = new Dictionary<string, int>();
            while (!ct.IsCancellationRequested)
            {
                var result = await cl.Get();
                if (result!.ClientId != null && observedVersions.ContainsKey(result!.ClientId!))
                    result.Timestamp.Should().BeGreaterOrEqualTo(observedVersions[result!.ClientId!]);

                if (result!.ClientId != null) observedVersions[result!.ClientId!] = result!.Timestamp!.Value;
            }
        }

        async Task WriteIndefinitely(AtomicRegistryClient cl, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested) await cl.Set($"{cl.ClientId}-{Guid.NewGuid()}");
        }

        var writersTask = Task.Run(
            async () => await Parallel.ForEachAsync(writerClients, parallelOptions,
                async (cl, _) => await WriteIndefinitely(cl, cancellationToken.Token)), cancellationToken.Token);

        var readersTask = Task.Run(
            async () => await Parallel.ForEachAsync(readerClients, parallelOptions,
                async (cl, _) => await AssertMonotonicWrites(cl, cancellationToken.Token)), cancellationToken.Token);

        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken.Token);

        cancellationToken.Cancel();
        await writersTask;
        await readersTask;
        await loggingTask;
    }

    [Test]
    public async Task ParallelReads_ReceiveSameResults()
    {
        var value1 = $"value1-{Guid.NewGuid()}";
        await client.Set(value1);

        Action[] tasks = Enumerable.Repeat(() =>
        {
            var result1 = client.Get().GetAwaiter().GetResult();
            result1!.Value.Should().Be(value1);
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
            result1!.Value.Should().Be(value1);
        }, 100).ToArray();

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 20 };
        Parallel.Invoke(parallelOptions, tasks);

        await client.InduceFault("Instance1", FaultSettingsDto.UnfreezeFrozenSets);
        await client.InduceFault("Instance3", FaultSettingsDto.UnfreezeFrozenSets);

        await setTask1;

        var result2 = await client.Get();
        result2!.Value.Should().Be(value1);
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

        result1!.Value.Should().Be(client2Value);
        result2!.Value.Should().Be(client2Value);
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

        result1!.Value.Should().Be(client2Value);
        result2!.Value.Should().Be(client2Value);
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
            .Select(x => CreateClient(x.ToString(), true))
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