namespace Orleans.Persistence.Couchbase.Serialization;

/// <summary>
/// Serialization format for Couchbase storage.
/// </summary>
public enum CouchbaseDataFormat : byte
{
    /// <summary>
    /// MessagePack binary format with 4-byte header (high performance).
    /// </summary>
    MessagePack = 1,

    /// <summary>
    /// Pure JSON format without header (operational visibility).
    /// </summary>
    Json = 2
}
