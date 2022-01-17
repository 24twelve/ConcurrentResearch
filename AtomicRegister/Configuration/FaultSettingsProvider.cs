using AtomicRegister.Common;
using AtomicRegister.Dto;
using Vostok.Configuration.Abstractions.SettingsTree;
using Vostok.Configuration.Sources.Manual;

namespace AtomicRegister.Configuration;

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