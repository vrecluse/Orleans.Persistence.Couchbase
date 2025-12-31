using System.ComponentModel.DataAnnotations;

namespace Orleans.Persistence.Couchbase.Configuration;

/// <summary>
/// Couchbase 存储配置选项
/// </summary>
public sealed class CouchbaseStorageOptions
{
    /// <summary>
    /// Couchbase 连接字符串（如 couchbase://localhost）
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 桶名称
    /// </summary>
    [Required]
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// 作用域名称（默认 _default）
    /// </summary>
    public string? ScopeName { get; set; }

    /// <summary>
    /// 集合名称（默认 _default）
    /// </summary>
    public string? CollectionName { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 密码
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 序列化器类型
    /// </summary>
    public SerializerType Serializer { get; set; } = SerializerType.Json;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int? MaxRetries { get; set; } = 3;

    /// <summary>
    /// 操作超时时间
    /// </summary>
    public TimeSpan? OperationTimeout { get; set; }

    /// <summary>
    /// 启用追踪
    /// </summary>
    public bool EnableTracing { get; set; } = false;

    /// <summary>
    /// 启用健康检查
    /// </summary>
    public bool EnableHealthCheck { get; set; } = true;
}
