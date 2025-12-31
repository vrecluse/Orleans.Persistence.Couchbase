namespace Orleans.Persistence.Couchbase.Core;

/// <summary>
/// Couchbase data manager interface for grain state persistence.
/// </summary>
public interface ICouchbaseDataManager : IAsyncDisposable
{
    /// <summary>
    /// Initialize connection to Couchbase cluster.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Read grain state from Couchbase.
    /// </summary>
    /// <typeparam name="T">The grain state type.</typeparam>
    /// <param name="grainType">The grain type name.</param>
    /// <param name="grainId">The grain identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of deserialized state (null if not found) and CAS value.</returns>
    Task<(T? State, ulong Cas)> ReadAsync<T>(
        string grainType,
        string grainId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Write grain state to Couchbase.
    /// </summary>
    /// <typeparam name="T">The grain state type.</typeparam>
    /// <param name="grainType">The grain type name.</param>
    /// <param name="grainId">The grain identifier.</param>
    /// <param name="state">The grain state to persist.</param>
    /// <param name="cas">CAS value for optimistic concurrency (0 for new documents).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>New CAS value after write.</returns>
    Task<ulong> WriteAsync<T>(
        string grainType,
        string grainId,
        T state,
        ulong cas,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete grain state from Couchbase.
    /// </summary>
    /// <param name="grainType">The grain type name.</param>
    /// <param name="grainId">The grain identifier.</param>
    /// <param name="cas">CAS value for optimistic concurrency (0 to skip check).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(
        string grainType,
        string grainId,
        ulong cas,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the bucket name.
    /// </summary>
    string BucketName { get; }
}
