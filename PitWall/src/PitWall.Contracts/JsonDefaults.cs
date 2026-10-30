using System.Text.Json;
using System.Text.Json.Serialization;

namespace PitWall.Contracts;

/// <summary>
/// Shared JSON serialiser options.
/// Wire protocol: camelCase, UTF-8.
/// </summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
