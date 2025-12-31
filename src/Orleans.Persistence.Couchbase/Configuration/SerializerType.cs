namespace Orleans.Persistence.Couchbase.Configuration;

/// <summary>
/// 序列化器类型
/// </summary>
public enum SerializerType
{
    /// <summary>
    /// JSON 序列化（System.Text.Json）
    /// </summary>
    Json,

    /// <summary>
    /// MessagePack 二进制序列化
    /// </summary>
    MessagePack
}
