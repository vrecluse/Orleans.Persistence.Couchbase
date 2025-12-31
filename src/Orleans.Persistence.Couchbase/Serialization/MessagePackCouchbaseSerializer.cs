using System.Buffers;
using MessagePack;

namespace Orleans.Persistence.Couchbase.Serialization;

/// <summary>
/// High-performance MessagePack serializer with 4-byte header for version control.
/// Used for high-frequency game data (player inventory, combat logs, etc.).
/// </summary>
public sealed class MessagePackCouchbaseSerializer : ICouchbaseSerializer
{
    private const byte Version = 1;
    private const int HeaderSize = 4;
    private readonly MessagePackSerializerOptions _options;

    public CouchbaseDataFormat Format => CouchbaseDataFormat.MessagePack;

    public MessagePackCouchbaseSerializer(MessagePackSerializerOptions? options = null)
    {
        _options = options ?? MessagePackSerializerOptions.Standard;
    }

    public void Serialize<T>(IBufferWriter<byte> writer, T value)
    {
        // Write 4-byte header for version control and format identification
        var header = writer.GetSpan(HeaderSize);
        header[0] = Version;
        header[1] = (byte)Format;
        header[2] = 0; // Reserved for compression flag
        header[3] = 0; // Reserved for encryption flag
        writer.Advance(HeaderSize);

        // Serialize payload using MessagePack
        MessagePackSerializer.Serialize(writer, value, _options);
    }

    public T Deserialize<T>(ReadOnlyMemory<byte> input)
    {
        if (input.Length < HeaderSize)
        {
            throw new InvalidDataException(
                $"Invalid MessagePack document: expected at least {HeaderSize} bytes header");
        }

        // Validate header
        var version = input.Span[0];
        if (version > Version)
        {
            throw new InvalidDataException(
                $"Unsupported MessagePack version: {version}. Maximum supported: {Version}");
        }

        // Deserialize payload (skip 4-byte header)
        return MessagePackSerializer.Deserialize<T>(input.Slice(HeaderSize), _options);
    }
}
