using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weather.Infrastructure.Serialization;

/// <summary>
/// Centralised <see cref="JsonSerializerOptions"/>.
/// <para>
/// Two concerns share one options object:
/// (1) deserialising the NWS wire format (camelCase, case-insensitive), and
/// (2) round-tripping the domain <c>Forecast</c> into the cache's JSON column.
/// Because the same options both write and read the cached payload, the exact
/// casing is an internal detail and never leaks to callers.
/// </para>
/// <para>
/// Reflection-based serialisation is used deliberately: it has zero risk of a
/// mis-declared source-generation context and is perfectly adequate for a
/// JIT-compiled server app. If you later publish Native AOT, add a
/// <see cref="JsonSerializerContext"/> and assign it to
/// <see cref="JsonSerializerOptions.TypeInfoResolver"/>.
/// </para>
/// </summary>
public static class WeatherJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };
}
