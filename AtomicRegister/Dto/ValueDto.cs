using Newtonsoft.Json;

namespace AtomicRegister.Dto;

[JsonObject(MemberSerialization.OptIn)]
public class ValueDto
{
    [JsonConstructor]
    private ValueDto()
    {
    }

    public ValueDto(int? timestamp, string? value, string? clientId)
    {
        Timestamp = timestamp;
        Value = value;
        ClientId = clientId;
    }

    public static ValueDto Empty => new(-1, null, null);

    [JsonProperty("timestamp")] public int? Timestamp { get; private set; }
    [JsonProperty("clientId")] public string? ClientId { get; private set; }
    [JsonProperty("value")] public string? Value { get; private set; }
}