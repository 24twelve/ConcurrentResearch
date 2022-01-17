using JetBrains.Annotations;

namespace AtomicRegister.Configuration;

[UsedImplicitly]
public class StorageSettings
{
    public IReadOnlyDictionary<string, string> InstanceNameFilePath { get; } = new Dictionary<string, string>();
}