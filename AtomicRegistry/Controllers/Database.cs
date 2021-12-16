using AtomicRegistry.Configuration;

namespace AtomicRegistry.Controllers;

public class Database
{
    private readonly object locker = new object();

    public Database(StorageSettings settings, string instanceName)
    {
        StorageFilePath = settings.InstanceNameFilePath[instanceName] ?? throw new ArgumentNullException();
    }

    private string StorageFilePath { get; }

    public void Write(string value)
    {
        lock (locker)
        {
            File.WriteAllText(StorageFilePath, value);
        }
    }

    public string Read()
    {
        return File.ReadAllText(StorageFilePath);
    }
}