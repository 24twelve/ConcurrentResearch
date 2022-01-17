namespace AtomicRegister.Configuration;

public class StorageSettings
{
    public IReadOnlyDictionary<string, string> InstanceNameFilePath { get; } = new Dictionary<string, string>();
}