using System.Buffers;
using System.Text.Json;

namespace Orleans.Persistence.Couchbase.Serialization;

/// <summary>
/// Pure JSON serializer without binary header for operational visibility.
/// Used for critical operational data (user accounts, billing records, etc.).
/// Documents remain readable in Couchbase Web Console and N1QL queries.
/// </summary>
public sealed class JsonCouchbaseSerializer : ICouchbaseSerializer
{
    private readonly JsonSerializerOptions _options;

    public CouchbaseDataFormat Format => CouchbaseDataFormat.Json;

    public JsonCouchbaseSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    public void Serialize<T>(IBufferWriter<byte> writer, T value)
    {
        // Write pure JSON without any binary header
        using var jsonWriter = new Utf8JsonWriter(writer);
        JsonSerializer.Serialize(jsonWriter, value, _options);
    }

    public T Deserialize<T>(ReadOnlyMemory<byte> input)
    {
        // Deserialize pure JSON
        return JsonSerializer.Deserialize<T>(input.Span, _options)!;
    }
}
