using System.Text;
using System.Text.Json;

namespace Orleans.Persistence.Couchbase.Serialization;

/// <summary>
/// 使用 System.Text.Json 的 JSON 序列化器
/// </summary>
public sealed class JsonGrainStateSerializer : IGrainStateSerializer
{
    private readonly JsonSerializerOptions _options;

    public string ContentType => "application/json; charset=utf-8";

    public JsonGrainStateSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public ReadOnlyMemory<byte> Serialize<T>(T grainState)
    {
        var json = JsonSerializer.Serialize(grainState, _options);
        return Encoding.UTF8.GetBytes(json);
    }

    public T Deserialize<T>(ReadOnlyMemory<byte> data)
    {
        var json = Encoding.UTF8.GetString(data.Span);
        return JsonSerializer.Deserialize<T>(json, _options)
            ?? throw new InvalidOperationException("Deserialization returned null");
    }
}
