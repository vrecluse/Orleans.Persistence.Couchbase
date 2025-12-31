using System.Buffers;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Orleans.Persistence.Couchbase.Serialization;

namespace Orleans.Persistence.Couchbase.Infrastructure;

/// <summary>
/// Smart transcoder that auto-detects JSON vs MessagePack format via first-byte inspection.
/// This solves the operational visibility problem: JSON documents remain pure for Web Console/N1QL queries,
/// while MessagePack documents get performance benefits from binary headers.
/// </summary>
public sealed class SmartCouchbaseTranscoder : ITypeTranscoder
{
    private readonly ICouchbaseSerializer _messagePackSerializer;
    private readonly ICouchbaseSerializer _jsonSerializer;

    /// <summary>
    /// Required by ITypeTranscoder but not used - we handle serialization directly.
    /// </summary>
    public ITypeSerializer? Serializer { get; set; }

    public SmartCouchbaseTranscoder(
        MessagePackCouchbaseSerializer messagePackSerializer,
        JsonCouchbaseSerializer jsonSerializer)
    {
        _messagePackSerializer = messagePackSerializer ?? throw new ArgumentNullException(nameof(messagePackSerializer));
        _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
    }

    /// <summary>
    /// Encoding is not used - DataManager handles serialization explicitly.
    /// </summary>
    public void Encode<T>(Stream stream, T value, Flags flags, OpCode opCode)
    {
        throw new NotSupportedException(
            "Use CouchbaseDataManager.WriteAsync with explicit format selection instead");
    }

    /// <summary>
    /// Decodes document with automatic format detection via first-byte inspection.
    /// </summary>
    public T? Decode<T>(ReadOnlyMemory<byte> buffer, Flags flags, OpCode opCode)
    {
        if (buffer.Length == 0)
        {
            return default;
        }

        var firstByte = buffer.Span[0];

        // Smart detection: JSON documents start with '{' (123) or '[' (91)
        if (firstByte == '{' || firstByte == '[')
        {
            return _jsonSerializer.Deserialize<T>(buffer);
        }

        // Otherwise, treat as MessagePack with binary header
        return _messagePackSerializer.Deserialize<T>(buffer);
    }

    /// <summary>
    /// Returns binary format flags (used for both JSON and MessagePack).
    /// </summary>
    public Flags GetFormat<T>(T value)
    {
        return new Flags
        {
            DataFormat = DataFormat.Binary
        };
    }
}
