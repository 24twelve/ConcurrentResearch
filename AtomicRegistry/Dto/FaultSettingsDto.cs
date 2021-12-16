using Newtonsoft.Json;

namespace AtomicRegistry.Dto;

public class FaultSettingsDto
{
    public static readonly FaultSettingsDto EverythingOk = new(false, false);
    public static readonly FaultSettingsDto Frozen = new(true, false);
    public static readonly FaultSettingsDto Down = new(false, true);

    [JsonConstructor]
    public FaultSettingsDto(bool isFrozen, bool isDown)
    {
        IsFrozen = isFrozen;
        IsDown = isDown;
    }

    public bool IsFrozen { get; }

    public bool
        IsDown { get; } //todo: кажется это ничем не отличается от IsFrozen для ситуации с бесконечными ретраями?
}