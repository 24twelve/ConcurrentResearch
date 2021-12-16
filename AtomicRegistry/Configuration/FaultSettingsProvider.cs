using AtomicRegistry.Common;
using AtomicRegistry.Dto;
using Vostok.Configuration.Abstractions.SettingsTree;
using Vostok.Configuration.Sources.Manual;

namespace AtomicRegistry.Configuration;

public class FaultSettingsProvider : ManualFeedSource<FaultSettingsDto>
{
    public FaultSettingsProvider(FaultSettingsDto initialSettings) : base(Parse)
    {
        Push(initialSettings);
    }

    private static ISettingsNode Parse(FaultSettingsDto input)
    {
        return new ValueNode(input.ToJson());
    }
}