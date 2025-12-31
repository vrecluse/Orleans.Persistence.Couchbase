namespace Orleans.Persistence.Couchbase.Core;

/// <summary>
/// Couchbase 数据管理器接口
/// </summary>
public interface ICouchbaseDataManager : IAsyncDisposable
{
    /// <summary>
    /// 初始化连接
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取粮食状态
    /// </summary>
    /// <param name="grainType">粮食类型</param>
    /// <param name="grainId">粮食ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据和 CAS 值的元组，如果不存在则返回空数据</returns>
    Task<(ReadOnlyMemory<byte> Data, ulong Cas)> ReadAsync(
        string grainType,
        string grainId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 写入粮食状态
    /// </summary>
    /// <param name="grainType">粮食类型</param>
    /// <param name="grainId">粮食ID</param>
    /// <param name="data">序列化数据</param>
    /// <param name="cas">CAS 值（0 表示新插入）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>新的 CAS 值</returns>
    Task<ulong> WriteAsync(
        string grainType,
        string grainId,
        ReadOnlyMemory<byte> data,
        ulong cas,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除粮食状态
    /// </summary>
    Task DeleteAsync(
        string grainType,
        string grainId,
        ulong cas,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 桶名称
    /// </summary>
    string BucketName { get; }
}
