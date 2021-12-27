using Newtonsoft.Json;

namespace AtomicRegistry.Dto;

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

    public static ValueDto Empty => new(-1, null);

    [JsonProperty("version")] public int? Version { get; private set; }

    [JsonProperty("value")] public string? Value { get; private set; }
}