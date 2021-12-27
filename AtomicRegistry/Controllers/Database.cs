using AtomicRegistry.Common;
using AtomicRegistry.Configuration;
using AtomicRegistry.Dto;

namespace AtomicRegistry.Controllers;

public class Database
{
    private readonly object locker = new();

    public Database(StorageSettings settings, string instanceName)
    {
        StorageFilePath = settings.InstanceNameFilePath[instanceName] ?? throw new ArgumentNullException();
    }

    private string StorageFilePath { get; }

    public void Write(ValueDto value)
    {
        lock (locker)
        {
            File.WriteAllText(StorageFilePath, value.ToJson());
        }
    }

    public ValueDto Read()
    {
        return File.ReadAllText(StorageFilePath).FromJson<ValueDto>() ?? ValueDto.Empty;
    }
}