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
        public async Task<string> Get()
        {
            ImitateFaults();

            return await System.IO.File.ReadAllTextAsync(StorageFilePath);
        }

        [HttpPost("set")]
        public void Set([FromBody] ValueDto value)
        {
            ImitateFaults();

            System.IO.File.WriteAllText(StorageFilePath, value.ToJson());
        }

        [HttpDelete("drop")]
        public void Drop()
        {
            System.IO.File.WriteAllText(StorageFilePath, string.Empty);
        }

        private void ImitateFaults()
        {
            if (faultSettingsObserver.CurrentSettings.IsDown) throw new Exception("Replica is down");
            while (faultSettingsObserver.CurrentSettings.IsFrozen) Thread.Sleep(5);
        }
    }
}