using AtomicRegistry.Common;
using AtomicRegistry.Configuration;
using AtomicRegistry.Dto;
using Microsoft.AspNetCore.Mvc;
using Vostok.Clusterclient.Core.Model;
using Vostok.Logging.Abstractions;

namespace AtomicRegistry.Controllers
{
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

            var currentVersion = database.Read().Version;
            if (value.Version <= currentVersion)
            {
                logger.Warn($"Cannot write {value.ToJson()}; current version {currentVersion} is higher");
                return StatusCode(409, $"Cannot write {value.ToJson()}; current version {currentVersion} is higher");
            }

            database.Write(value);
            return StatusCode(200);
        }

        [HttpDelete("drop")]
        public IActionResult Drop()
        {
            //todo: раскостылить на нормальную очередь запросов или какой-то механизм asp net core
            faultSettingsProvider.Push(FaultSettingsDto.EverythingOk);
            Thread.Sleep(TimeSpan.FromSeconds(5));
            database.Write(ValueDto.Empty);
            return StatusCode(200);
        }
    }
}