﻿using System.Diagnostics;

namespace AtomicRegister.Configuration;

public static class ThreadPoolUtility
{
    private const int MaximumThreads = 32767;

    public static void SetUp(int multiplier = 32)
    {
        if (multiplier <= 0)
            throw new ArgumentException($"Unable to setup minimum threads with multiplier: {multiplier}");

        var minimumThreads = Math.Min(Environment.ProcessorCount * multiplier, MaximumThreads);
        ThreadPool.SetMaxThreads(MaximumThreads, MaximumThreads);
        ThreadPool.SetMinThreads(minimumThreads, minimumThreads);
    }

    public static ThreadPoolState GetThreadPoolState()
    {
        ThreadPool.GetMinThreads(out var minWorkerThreads, out var minIocpThreads);
        ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxIocpThreads);
        ThreadPool.GetAvailableThreads(out var availableWorkerThreads, out var availableIocpThread);

        var usedWorkerThreads = maxWorkerThreads - availableWorkerThreads;
        var usedIocpThreads = maxIocpThreads - availableIocpThread;
        var processThreadsCount = Process.GetCurrentProcess().Threads.Count;
        return new ThreadPoolState(minWorkerThreads, usedWorkerThreads, minIocpThreads, usedIocpThreads,
            processThreadsCount);
    }
}

public class ThreadPoolState
{
    public ThreadPoolState(int minWorkerThreads, int usedWorkerThreads, int minIocpThreads, int usedIocpThreads,
        int processThreadsCount)
    {
        MinWorkerThreads = minWorkerThreads;
        UsedWorkerThreads = usedWorkerThreads;
        MinIocpThreads = minIocpThreads;
        UsedIocpThreads = usedIocpThreads;
        ProcessThreadsCount = processThreadsCount;
    }

    private int MinWorkerThreads { get; }
    private int UsedWorkerThreads { get; }
    private int MinIocpThreads { get; }
    private int UsedIocpThreads { get; }
    private int ProcessThreadsCount { get; }

    public override string ToString()
    {
        return
            $"MinWorkerThreads: {MinWorkerThreads}, UsedWorkerThreads: {UsedWorkerThreads}, MinIocpThreads: {MinIocpThreads}, UsedIocpThreads: {UsedIocpThreads}, ProcessThreadsCount: {ProcessThreadsCount}";
    }
}