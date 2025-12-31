using System.Buffers;

namespace Orleans.Persistence.Couchbase.Serialization;

/// <summary>
/// Abstraction for Couchbase document serialization.
/// </summary>
public interface ICouchbaseSerializer
{
    /// <summary>
    /// Gets the serialization format.
    /// </summary>
    CouchbaseDataFormat Format { get; }

    /// <summary>
    /// Serializes a value to the buffer writer.
    /// </summary>
    void Serialize<T>(IBufferWriter<byte> writer, T value);

    /// <summary>
    /// Deserializes a value from memory.
    /// </summary>
    T Deserialize<T>(ReadOnlyMemory<byte> input);
}
