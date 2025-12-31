using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;

namespace Orleans.Persistence.Couchbase.Infrastructure;

/// <summary>
/// Raw binary transcoder that writes byte[] directly without additional serialization.
/// Used internally by DataManager to send pre-serialized data to Couchbase.
/// </summary>
internal sealed class RawBinaryTranscoder : ITypeTranscoder
{
    public ITypeSerializer? Serializer { get; set; }

    public void Encode<T>(Stream stream, T value, Flags flags, OpCode opCode)
    {
        if (value is byte[] bytes)
        {
            stream.Write(bytes);
        }
        else
        {
            throw new InvalidOperationException(
                "RawBinaryTranscoder only supports byte[] input");
        }
    }

    public T? Decode<T>(ReadOnlyMemory<byte> buffer, Flags flags, OpCode opCode)
    {
        if (typeof(T) == typeof(byte[]))
        {
            return (T)(object)buffer.ToArray();
        }

        throw new InvalidOperationException(
            "RawBinaryTranscoder only supports byte[] output");
    }

    public Flags GetFormat<T>(T value)
    {
        return new Flags
        {
            DataFormat = DataFormat.Binary
        };
    }
}
