using Couchbase;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;
using Orleans.Persistence.Couchbase.Configuration;
using Orleans.Persistence.Couchbase.Infrastructure;
using Orleans.Storage;

namespace Orleans.Persistence.Couchbase.Core;

/// <summary>
/// High-performance Couchbase data manager using custom transcoder for zero-copy serialization.
/// </summary>
public sealed class CouchbaseDataManager : ICouchbaseDataManager
{
    private readonly ICluster _cluster;
    private readonly CouchbaseStorageOptions _options;
    private readonly ILogger<CouchbaseDataManager> _logger;
    private readonly ITypeTranscoder _transcoder;
    private ICouchbaseCollection? _collection;

    public string BucketName => _options.BucketName;

    public CouchbaseDataManager(
        ICluster cluster,
        CouchbaseStorageOptions options,
        ITypeTranscoder transcoder,
        ILogger<CouchbaseDataManager> logger)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _transcoder = transcoder ?? throw new ArgumentNullException(nameof(transcoder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var bucket = await _cluster.BucketAsync(_options.BucketName);
            var scope = bucket.Scope(_options.ScopeName ?? "_default");
            _collection = scope.Collection(_options.CollectionName ?? "_default");

            _logger.LogInformation(
                "Initialized Couchbase storage: Bucket={Bucket}, Scope={Scope}, Collection={Collection}",
                _options.BucketName,
                _options.ScopeName ?? "_default",
                _options.CollectionName ?? "_default");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Couchbase connection");
            throw;
        }
    }

    public async Task<(T? State, ulong Cas)> ReadAsync<T>(
        string grainType,
        string grainId,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var key = BuildDocumentKey(grainType, grainId);

        try
        {
            var result = await _collection!.GetAsync(key, options =>
                options.Transcoder(_transcoder));

            // Transcoder handles deserialization internally via ContentAs<T>
            var state = result.ContentAs<T>();
            return (state, result.Cas);
        }
        catch (global::Couchbase.Core.Exceptions.KeyValue.DocumentNotFoundException)
        {
            return (default, 0);
        }
    }

    public async Task<ulong> WriteAsync<T>(
        string grainType,
        string grainId,
        T state,
        ulong cas,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var key = BuildDocumentKey(grainType, grainId);

        try
        {
            IMutationResult result;

            if (cas != 0)
            {
                // Use ReplaceAsync with CAS for optimistic concurrency control
                var replaceOptions = new ReplaceOptions()
                    .Cas(cas)
                    .Transcoder(_transcoder);
                result = await _collection!.ReplaceAsync(key, state, replaceOptions);
            }
            else
            {
                // Use UpsertAsync for new documents
                var upsertOptions = new UpsertOptions()
                    .Transcoder(_transcoder);
                result = await _collection!.UpsertAsync(key, state, upsertOptions);
            }

            return result.Cas;
        }
        catch (global::Couchbase.Core.Exceptions.CasMismatchException)
        {
            throw new InconsistentStateException(
                "ETag mismatch - concurrent modification detected",
                cas.ToString(),
                "unknown");
        }
    }

    public async Task DeleteAsync(
        string grainType,
        string grainId,
        ulong cas,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var key = BuildDocumentKey(grainType, grainId);

        try
        {
            if (cas != 0)
            {
                var options = new RemoveOptions().Cas(cas);
                await _collection!.RemoveAsync(key, options);
            }
            else
            {
                await _collection!.RemoveAsync(key);
            }
        }
        catch (global::Couchbase.Core.Exceptions.KeyValue.DocumentNotFoundException)
        {
            // Document doesn't exist - treat as successful deletion
        }
        catch (global::Couchbase.Core.Exceptions.CasMismatchException)
        {
            throw new InconsistentStateException(
                "ETag mismatch during delete",
                cas.ToString(),
                "unknown");
        }
    }

    /// <summary>
    /// DataManager does not own the ICluster lifecycle - managed by DI container.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        // No-op: ICluster lifecycle is managed by DI container
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Builds document key using optimized string.Create for minimal allocations.
    /// </summary>
    private static string BuildDocumentKey(string grainType, string grainId)
    {
        return string.Create(
            grainType.Length + 1 + grainId.Length,
            (grainType, grainId),
            static (span, state) =>
            {
                state.grainType.AsSpan().CopyTo(span);
                span[state.grainType.Length] = ':';
                state.grainId.AsSpan().CopyTo(span[(state.grainType.Length + 1)..]);
            });
    }

    private void EnsureInitialized()
    {
        if (_collection == null)
        {
            throw new InvalidOperationException(
                "CouchbaseDataManager not initialized. Call InitializeAsync first.");
        }
    }
}
