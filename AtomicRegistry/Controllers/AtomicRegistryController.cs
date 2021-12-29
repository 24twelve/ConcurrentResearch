using AtomicRegistry.Common;
using AtomicRegistry.Configuration;
using AtomicRegistry.Dto;
using Microsoft.AspNetCore.Mvc;
using Vostok.Logging.Abstractions;

namespace AtomicRegistry.Controllers;

[ApiController]
[Route("/api")]
public class AtomicRegistryController : ControllerBase
{
    private readonly Database database;
    private readonly FaultSettingsObserver faultSettingsObserver;
    private readonly FaultSettingsProvider faultSettingsProvider;
    private readonly ILog logger;

    public AtomicRegistryController(
        FaultSettingsObserver faultSettingsObserver,
        FaultSettingsProvider faultSettingsProvider,
        Database database,
        ILog logger)
    {
        this.faultSettingsObserver = faultSettingsObserver;
        this.faultSettingsProvider = faultSettingsProvider;
        this.database = database;
        this.logger = logger;
    }


    [HttpGet]
    public IActionResult Get()
    {
        while (faultSettingsObserver.CurrentSettings.IsGetFrozen) Thread.Sleep(5);

        return StatusCode(200, database.Read().ToJson());
    }

    [HttpPost("set")]
    public IActionResult Set([FromBody] ValueDto value)
    {
        while (faultSettingsObserver.CurrentSettings.IsSetFrozen) Thread.Sleep(5);
        if (faultSettingsObserver.CurrentSettings.NextSetFrozen)
        {
            faultSettingsObserver.CurrentSettings.NextSetFrozen = false;
            while (!faultSettingsObserver.CurrentSettings.ShouldUnfreezeFrozenSets) Thread.Sleep(5);
        }

        var currentValue = database.Read();
        var currentTimestamp = currentValue.Timestamp;
        if (value.Timestamp == currentTimestamp && currentValue.ClientId != value.ClientId)
        {
            logger.Warn($"Received {value.ToJson()}; current timestamp {currentTimestamp} is equal. Replacing...");
            database.Write(value);
            return StatusCode(200);
        }

        if (value.Timestamp == currentTimestamp && currentValue.ClientId == value.ClientId)
        {
            logger.Warn($"Value {value.ToJson()} is current and already exists");
            return StatusCode(200);
        }

        if (value.Timestamp < currentTimestamp)
        {
            var errorMessage =
                $"Cannot write {value.ToJson()}; current timestamp {currentValue.ToJson()} is higher or equal but from same writer";
            logger.Warn(errorMessage);
            return StatusCode(409, errorMessage);
        }

        database.Write(value);
        return StatusCode(200);
    }

    [HttpDelete("drop")]
    public IActionResult Drop()
    {
        //todo: раскостылить на нормальную очередь запросов или какой-то механизм asp net core или cancellation token на drop
        //todo: кажется, эта штука работает реально медленно и 5 секунд не хватат
        faultSettingsProvider.Push(FaultSettingsDto.EverythingOk);
        Thread.Sleep(TimeSpan.FromSeconds(10));
        database.Write(ValueDto.Empty);
        return StatusCode(200);
    }
}