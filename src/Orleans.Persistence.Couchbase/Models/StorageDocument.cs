namespace Orleans.Persistence.Couchbase.Models;

/// <summary>
/// Couchbase 存储文档包装器
/// </summary>
public sealed class StorageDocument
{
    /// <summary>
    /// Base64 编码的序列化数据
    /// </summary>
    public required string Data { get; init; }

    /// <summary>
    /// 内容类型（如 application/json 或 application/x-msgpack）
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// 文档格式版本
    /// </summary>
    public int Version { get; init; } = 2;
}
