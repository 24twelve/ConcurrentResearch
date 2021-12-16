using AtomicRegistry.Common;
using AtomicRegistry.Configuration;
using AtomicRegistry.Dto;
using Microsoft.AspNetCore.Mvc;

namespace AtomicRegistry.Controllers
{
    [ApiController]
    [Route("/api")]
    public class AtomicRegistryController : ControllerBase
    {
        private readonly FaultSettingsObserver faultSettingsObserver;


        public AtomicRegistryController(
            StorageSettings settings,
            InstanceName instanceName,
            FaultSettingsObserver faultSettingsObserver)
        {
            this.faultSettingsObserver = faultSettingsObserver;
            StorageFilePath = settings.InstanceNameFilePath[instanceName.Value] ?? throw new ArgumentNullException();
        }

        private string StorageFilePath { get; }

        [HttpGet]
        public string Get()
        {
            if (faultSettingsObserver.CurrentSettings.IsDown) throw new Exception("Replica is down");
            while (faultSettingsObserver.CurrentSettings.IsFrozen) Thread.Sleep(5);

            return System.IO.File.ReadAllText(StorageFilePath);
        }

        [HttpPost("set")]
        public void Set([FromBody] ValueDto value)
        {
            if (faultSettingsObserver.CurrentSettings.IsDown) throw new Exception("Replica is down");
            while (faultSettingsObserver.CurrentSettings.IsFrozen) Thread.Sleep(5);

            System.IO.File.WriteAllText(StorageFilePath, value.ToJson());
        }

        [HttpDelete("drop")]
        public void Drop()
        {
            System.IO.File.WriteAllText(StorageFilePath, string.Empty);
        }
    }
}