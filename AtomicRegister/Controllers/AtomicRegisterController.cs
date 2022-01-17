using AtomicRegister.Common;
using AtomicRegister.Configuration;
using AtomicRegister.Dto;
using Microsoft.AspNetCore.Mvc;
using Vostok.Logging.Abstractions;

namespace AtomicRegister.Controllers;

[ApiController]
[Route("/api")]
public class AtomicRegisterController : ControllerBase
{
    private readonly Database database;
    private readonly FaultSettingsObserver faultSettingsObserver;
    private readonly FaultSettingsProvider faultSettingsProvider;
    private readonly ILog logger;
    private readonly ConcurrentCounter concurrentCounter;

    public AtomicRegisterController(
        FaultSettingsObserver faultSettingsObserver,
        FaultSettingsProvider faultSettingsProvider,
        Database database,
        ConcurrentCounter concurrentCounter,
        ILog logger)
    {
        this.faultSettingsObserver = faultSettingsObserver;
        this.faultSettingsProvider = faultSettingsProvider;
        this.database = database;
        this.concurrentCounter = concurrentCounter;
        this.logger = logger;
    }


    [HttpGet]
    public IActionResult Get()
    {
        concurrentCounter.Increment();
        while (faultSettingsObserver.CurrentSettings.IsGetFrozen) Thread.Sleep(5);

        try
        {
            return StatusCode(200, database.Read().ToJson());
        }
        finally
        {
            concurrentCounter.Decrement();
        }
    }

    [HttpPost("set")]
    public IActionResult Set([FromBody] ValueDto value)
    {
        concurrentCounter.Increment();

        while (faultSettingsObserver.CurrentSettings.IsSetFrozen) Thread.Sleep(5);
        if (faultSettingsObserver.CurrentSettings.NextSetFrozen)
        {
            faultSettingsObserver.CurrentSettings.NextSetFrozen = false;
            while (!faultSettingsObserver.CurrentSettings.ShouldUnfreezeFrozenSets) Thread.Sleep(5);
        }

        if (!database.CompareAndSet(value, out var current))
        {
            var conflictMessage = $"Cannot write {value.ToJson()}; current timestamp {current.Timestamp} is equal";
            logger.Warn(conflictMessage);
            try
            {
                return StatusCode(409, conflictMessage);
            }
            finally
            {
                concurrentCounter.Decrement();
            }
        }

        logger.Info($"Writing {value.ToJson()}");
        try
        {
            return StatusCode(200);
        }
        finally
        {
            concurrentCounter.Decrement();
        }
    }

    [HttpDelete("drop")]
    public IActionResult Drop()
    {
        faultSettingsProvider.Push(FaultSettingsDto.EverythingOk);
        int? runningRequests = null;
        while (runningRequests is null or > 0)
        {
            runningRequests = concurrentCounter.CurrentCount;
            logger.Info($"Waiting for all requests to finish. Pending requests: {runningRequests}");
            Thread.Sleep(TimeSpan.FromMilliseconds(1));
        }

        database.Write(ValueDto.Empty);
        return StatusCode(200);
    }
}