using System.Collections.Generic;

namespace AtomicRegistry.Configuration
{
    public class StorageSettings
    {
        public Dictionary<string, string> InstanceNameFilePath { get; } = new Dictionary<string, string>();
    }
}