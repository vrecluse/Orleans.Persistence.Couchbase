namespace Orleans.Persistence.Couchbase.Serialization;

/// <summary>
/// 粮食状态序列化器接口 - 二进制优先设计
/// </summary>
public interface IGrainStateSerializer
{
    /// <summary>
    /// 将粮食状态序列化为字节数组
    /// </summary>
    ReadOnlyMemory<byte> Serialize<T>(T grainState);

    /// <summary>
    /// 从字节数组反序列化粮食状态
    /// </summary>
    T Deserialize<T>(ReadOnlyMemory<byte> data);

    /// <summary>
    /// 序列化器的内容类型标识
    /// </summary>
    string ContentType { get; }
}
