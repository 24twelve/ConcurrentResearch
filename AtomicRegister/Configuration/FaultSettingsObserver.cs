using AtomicRegister.Common;
using AtomicRegister.Dto;
using Vostok.Configuration.Abstractions.SettingsTree;
using IConfigurationSource = Vostok.Configuration.Abstractions.IConfigurationSource;

namespace AtomicRegister.Configuration;

public class FaultSettingsObserver : IObserver<(ISettingsNode, Exception)>
{
    private readonly IDisposable unsubscriber;

    public FaultSettingsObserver(IConfigurationSource settingsProvider)
    {
        unsubscriber = settingsProvider.Observe().Subscribe(this);
    }

    public FaultSettingsDto CurrentSettings { get; private set; } = FaultSettingsDto.EverythingOk;

    public void OnCompleted()
    {
        unsubscriber.Dispose();
    }

    public void OnError(Exception error)
    {
        throw error;
    }

    public void OnNext((ISettingsNode, Exception) value)
    {
        CurrentSettings = value.Item1.Value?.FromJson<FaultSettingsDto>() ??
                          throw new ArgumentNullException(nameof(value));
    }
}