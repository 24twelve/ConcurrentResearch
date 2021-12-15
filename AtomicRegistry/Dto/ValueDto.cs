using Newtonsoft.Json;

namespace AtomicRegistry.Client;

[JsonObject(MemberSerialization.OptIn)]
public class ValueDto
{
    [JsonConstructor]
    private ValueDto()
    {
    }

    public ValueDto(int? version, string? value)
    {
        Version = version;
        Value = value;
    }

    [JsonProperty("version")] public int? Version { get; private set; }

    [JsonProperty("value")] public string? Value { get; private set; }
}