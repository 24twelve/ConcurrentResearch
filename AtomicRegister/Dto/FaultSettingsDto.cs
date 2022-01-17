using Newtonsoft.Json;

namespace AtomicRegister.Dto;

public class FaultSettingsDto
{
    public static readonly FaultSettingsDto EverythingOk = new(false, false, false, true);
    public static readonly FaultSettingsDto AllFrozen = new(true, true, false, false);
    public static readonly FaultSettingsDto OneSetFrozen = new(false, false, true, false);
    public static readonly FaultSettingsDto UnfreezeFrozenSets = new(false, false, false, true);

    [JsonConstructor]
    public FaultSettingsDto(bool isGetFrozen, bool isSetFrozen, bool nextSetFrozen, bool shouldUnfreezeFrozenSets)
    {
        IsGetFrozen = isGetFrozen;
        IsSetFrozen = isSetFrozen;
        NextSetFrozen = nextSetFrozen;
        ShouldUnfreezeFrozenSets = shouldUnfreezeFrozenSets;
    }

    public bool IsGetFrozen { get; }
    public bool IsSetFrozen { get; }
    public bool NextSetFrozen { get; set; }
    public bool ShouldUnfreezeFrozenSets { get; }
}