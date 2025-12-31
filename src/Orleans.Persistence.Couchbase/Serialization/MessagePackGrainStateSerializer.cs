using MessagePack;

namespace Orleans.Persistence.Couchbase.Serialization;

/// <summary>
/// 使用 MessagePack 的二进制序列化器
/// </summary>
public sealed class MessagePackGrainStateSerializer : IGrainStateSerializer
{
    private readonly MessagePackSerializerOptions _options;

    public string ContentType => "application/x-msgpack";

    public MessagePackGrainStateSerializer(MessagePackSerializerOptions? options = null)
    {
        _options = options ?? MessagePackSerializerOptions.Standard;
    }

    public ReadOnlyMemory<byte> Serialize<T>(T grainState)
    {
        return MessagePackSerializer.Serialize(grainState, _options);
    }

    public T Deserialize<T>(ReadOnlyMemory<byte> data)
    {
        return MessagePackSerializer.Deserialize<T>(data, _options);
    }
}
