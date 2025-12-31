using System.Buffers;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using CommunityToolkit.HighPerformance.Buffers;
using MessagePack;

namespace Orleans.Persistence.Couchbase.Infrastructure;

/// <summary>
/// High-performance zero-copy transcoder for Orleans grain state storage.
/// Uses ArrayPoolBufferWriter to minimize GC pressure and MessagePack for efficient serialization.
/// </summary>
/// <remarks>
/// Binary format:
/// - Byte 0: Version (currently 2)
/// - Byte 1: Format (1 = MessagePack)
/// - Bytes 2-3: Reserved (for future compression/encryption flags)
/// - Bytes 4+: MessagePack-serialized payload
/// </remarks>
public sealed class OrleansCouchbaseTranscoder : ITypeTranscoder
{
    private const byte CurrentVersion = 2;
    private const byte FormatMessagePack = 1;
    private const int HeaderSize = 4;
    private const int DefaultBufferSize = 1024;

    private readonly MessagePackSerializerOptions _serializerOptions;

    public OrleansCouchbaseTranscoder(MessagePackSerializerOptions? serializerOptions = null)
    {
        _serializerOptions = serializerOptions ?? MessagePackSerializerOptions.Standard;
    }

    /// <summary>
    /// Required by ITypeTranscoder but not used - we handle serialization directly.
    /// </summary>
    public ITypeSerializer? Serializer { get; set; }

    /// <summary>
    /// Encodes a grain state object to binary format with 4-byte header.
    /// Uses pooled memory to minimize allocations.
    /// </summary>
    public void Encode<T>(Stream stream, T value, Flags flags, OpCode opCode)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var writer = new ArrayPoolBufferWriter<byte>(DefaultBufferSize);

        // Write 4-byte header
        var headerSpan = writer.GetSpan(HeaderSize);
        headerSpan[0] = CurrentVersion;
        headerSpan[1] = FormatMessagePack;
        headerSpan[2] = 0; // Reserved for compression
        headerSpan[3] = 0; // Reserved for encryption
        writer.Advance(HeaderSize);

        // Serialize directly to pooled buffer (zero intermediate allocations)
        MessagePackSerializer.Serialize(writer, value, _serializerOptions);

        // Write to output stream
        stream.Write(writer.WrittenSpan);
    }

    /// <summary>
    /// Decodes binary data back to grain state object.
    /// Zero-copy: deserializes directly from input buffer without intermediate allocations.
    /// </summary>
    public T? Decode<T>(ReadOnlyMemory<byte> buffer, Flags flags, OpCode opCode)
    {
        if (buffer.Length == 0)
        {
            return default;
        }

        if (buffer.Length < HeaderSize)
        {
            throw new InvalidDataException(
                $"Invalid document header: expected at least {HeaderSize} bytes, got {buffer.Length}");
        }

        // Read header using Span (zero-cost operation)
        var span = buffer.Span;
        var version = span[0];
        var format = span[1];

        // Version validation (forward compatible)
        if (version > CurrentVersion)
        {
            throw new InvalidDataException(
                $"Unsupported document version: {version}. Maximum supported: {CurrentVersion}");
        }

        // Format validation
        if (format != FormatMessagePack)
        {
            throw new InvalidDataException(
                $"Unsupported serialization format: {format}. Expected: {FormatMessagePack} (MessagePack)");
        }

        // Slice to get payload (pointer offset only, no memory copy)
        var payload = buffer.Slice(HeaderSize);

        // Deserialize directly from input memory (zero-copy)
        return MessagePackSerializer.Deserialize<T>(payload, _serializerOptions);
    }

    /// <summary>
    /// Gets the flags indicating binary format.
    /// </summary>
    public Flags GetFormat<T>(T value)
    {
        return new Flags
        {
            DataFormat = DataFormat.Binary
        };
    }
}
