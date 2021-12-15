using Newtonsoft.Json;

namespace AtomicRegistry.Common;

public static class JsonExtensions
{
    public static string ToJson<T>(this T obj)
    {
        return JsonConvert.SerializeObject(obj);
    }

    public static T? FromJson<T>(this string serialized)
    {
        return JsonConvert.DeserializeObject<T>(serialized);
    }
}