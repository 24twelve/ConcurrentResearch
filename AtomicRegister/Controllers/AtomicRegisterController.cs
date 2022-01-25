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
    private readonly ConcurrentCounter concurrentCounter;
    private readonly Database database;
    private readonly FaultSettingsObserver faultSettingsObserver;
    private readonly FaultSettingsProvider faultSettingsProvider;
    private readonly ILog logger;

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
        using (concurrentCounter.TakeLease())
        {
            while (faultSettingsObserver.CurrentSettings.IsGetFrozen) Thread.Sleep(5);
            return StatusCode(200, database.Read().ToJson());
        }
    }

    [HttpPost("set")]
    public IActionResult Set([FromBody] ValueDto value)
    {
        using (concurrentCounter.TakeLease())
        {
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
                return StatusCode(409, conflictMessage);
            }

            logger.Info($"Writing {value.ToJson()}");
            return StatusCode(200);
        }
    }

    [HttpDelete("drop")]
    public IActionResult Drop()
    {
        faultSettingsProvider.Push(FaultSettingsDto.EverythingOk);
        int? runningRequests = null;
        while (runningRequests is null or > 0)
        {
            runningRequests = concurrentCounter.LeaseCount;
            logger.Info($"Waiting for all requests to finish. Pending requests: {runningRequests}");
            Thread.Sleep(TimeSpan.FromMilliseconds(1));
        }

        database.Write(ValueDto.Empty);
        return StatusCode(200);
    }
}