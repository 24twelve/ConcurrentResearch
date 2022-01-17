using AtomicRegister.Common;
using AtomicRegister.Configuration;
using AtomicRegister.Dto;

namespace AtomicRegister.Controllers;

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
        //todo: but why? i thought lock is not needed
        lock (locker)
        {
            return File.ReadAllText(StorageFilePath).FromJson<ValueDto>() ?? ValueDto.Empty;
        }
    }

    public bool CompareAndSet(ValueDto next, out ValueDto current)
    {
        lock (locker)
        {
            current = File.ReadAllText(StorageFilePath).FromJson<ValueDto>() ?? ValueDto.Empty;
            if (next.Timestamp > current.Timestamp)
            {
                File.WriteAllText(StorageFilePath, next.ToJson());
                return true;
            }

            return false;
        }
    }
}