using System;
using AtomicRegistry.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace AtomicRegistry.Controllers
{
    [ApiController]
    [Route("/api")]
    public class AtomicRegistryController : ControllerBase
    {
        public AtomicRegistryController(StorageSettings settings, InstanceName instanceName)
        {
            StorageFilePath = settings.InstanceNameFilePath[instanceName.Value] ?? throw new ArgumentNullException();
        }

        private string StorageFilePath { get; }

        [HttpGet]
        public string Get()
        {
            return System.IO.File.ReadAllText(StorageFilePath);
        }

        [HttpPost("set")]
        public void Set(string value)
        {
            System.IO.File.WriteAllText(StorageFilePath, value);
        }
    }
}