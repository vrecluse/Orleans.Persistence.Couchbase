using Couchbase;
using Couchbase.KeyValue;
using CommunityToolkit.HighPerformance.Buffers;
using Microsoft.Extensions.Logging;
using Orleans.Persistence.Couchbase.Configuration;
using Orleans.Persistence.Couchbase.Infrastructure;
using Orleans.Persistence.Couchbase.Serialization;
using Orleans.Storage;

namespace Orleans.Persistence.Couchbase.Core;

/// <summary>
/// High-performance Couchbase data manager with smart format selection.
/// Supports both MessagePack (high performance) and JSON (operational visibility).
/// </summary>
public sealed class CouchbaseDataManager : ICouchbaseDataManager
{
    private readonly ICluster _cluster;
    private readonly CouchbaseStorageOptions _options;
    private readonly ILogger<CouchbaseDataManager> _logger;
    private readonly SmartCouchbaseTranscoder _smartTranscoder;
    private readonly MessagePackCouchbaseSerializer _messagePackSerializer;
    private readonly JsonCouchbaseSerializer _jsonSerializer;
    private ICouchbaseCollection? _collection;

    public string BucketName => _options.BucketName;

    public CouchbaseDataManager(
        ICluster cluster,
        CouchbaseStorageOptions options,
        MessagePackCouchbaseSerializer messagePackSerializer,
        JsonCouchbaseSerializer jsonSerializer,
        SmartCouchbaseTranscoder smartTranscoder,
        ILogger<CouchbaseDataManager> logger)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _messagePackSerializer = messagePackSerializer ?? throw new ArgumentNullException(nameof(messagePackSerializer));
        _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
        _smartTranscoder = smartTranscoder ?? throw new ArgumentNullException(nameof(smartTranscoder));
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
            // SmartTranscoder automatically detects JSON vs MessagePack format
            var result = await _collection!.GetAsync(key, options =>
                options.Transcoder(_smartTranscoder));

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
        return await WriteAsync(grainType, grainId, state, cas, _options.DefaultDataFormat, cancellationToken);
    }

    /// <summary>
    /// Writes grain state with explicit format selection.
    /// </summary>
    public async Task<ulong> WriteAsync<T>(
        string grainType,
        string grainId,
        T state,
        ulong cas,
        CouchbaseDataFormat format,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var key = BuildDocumentKey(grainType, grainId);

        // Use ArrayPoolBufferWriter for zero-allocation serialization
        using var writer = new ArrayPoolBufferWriter<byte>(2048);

        // Select serializer based on format
        var serializer = format == CouchbaseDataFormat.MessagePack
            ? _messagePackSerializer
            : (ICouchbaseSerializer)_jsonSerializer;

        serializer.Serialize(writer, state);

        // Convert to array for Couchbase SDK (final allocation point)
        var dataToSend = writer.WrittenMemory.ToArray();

        try
        {
            IMutationResult result;
            var rawTranscoder = new RawBinaryTranscoder();

            if (cas != 0)
            {
                // Use ReplaceAsync with CAS for optimistic concurrency control
                var replaceOptions = new ReplaceOptions()
                    .Cas(cas)
                    .Transcoder(rawTranscoder);
                result = await _collection!.ReplaceAsync(key, dataToSend, replaceOptions);
            }
            else
            {
                // Use UpsertAsync for new documents
                var upsertOptions = new UpsertOptions()
                    .Transcoder(rawTranscoder);
                result = await _collection!.UpsertAsync(key, dataToSend, upsertOptions);
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
