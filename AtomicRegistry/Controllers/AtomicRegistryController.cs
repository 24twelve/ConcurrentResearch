using AtomicRegistry.Common;
using AtomicRegistry.Configuration;
using AtomicRegistry.Dto;
using Microsoft.AspNetCore.Mvc;
using Vostok.Clusterclient.Core.Model;

namespace AtomicRegistry.Controllers
{
    [ApiController]
    [Route("/api")]
    public class AtomicRegistryController : ControllerBase
    {
        private readonly Database database;
        private readonly FaultSettingsObserver faultSettingsObserver;

        public AtomicRegistryController(
            FaultSettingsObserver faultSettingsObserver, Database database)
        {
            this.faultSettingsObserver = faultSettingsObserver;
            this.database = database;
        }


        [HttpGet]
        public string Get()
        {
            while (faultSettingsObserver.CurrentSettings.IsGetFrozen) Thread.Sleep(5);

            return database.Read();
        }

        [HttpPost("set")]
        public void Set([FromBody] ValueDto value)
        {
            while (faultSettingsObserver.CurrentSettings.IsSetFrozen) Thread.Sleep(5);
            if (faultSettingsObserver.CurrentSettings.NextSetFrozen)
            {
                faultSettingsObserver.CurrentSettings.NextSetFrozen = false;
                while (!faultSettingsObserver.CurrentSettings.ShouldUnfreezeFrozenSets) Thread.Sleep(5);
            }

            database.Write(value.ToJson());
        }

        [HttpDelete("drop")]
        public void Drop()
        {
            database.Write(string.Empty);
        }
    }
}