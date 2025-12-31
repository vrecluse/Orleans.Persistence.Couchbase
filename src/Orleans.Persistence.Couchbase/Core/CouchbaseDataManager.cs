using Couchbase;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;
using Orleans.Persistence.Couchbase.Configuration;
using Orleans.Persistence.Couchbase.Models;
using Orleans.Persistence.Couchbase.Serialization;
using Orleans.Storage;
using Polly;
using Polly.Retry;

namespace Orleans.Persistence.Couchbase.Core;

/// <summary>
/// Couchbase SDK 3.x 数据管理器实现
/// </summary>
public sealed class CouchbaseDataManager : ICouchbaseDataManager
{
    private readonly ICluster _cluster;
    private readonly CouchbaseStorageOptions _options;
    private readonly ILogger<CouchbaseDataManager> _logger;
    private readonly IGrainStateSerializer _serializer;
    private readonly AsyncRetryPolicy _retryPolicy;
    private ICouchbaseCollection? _collection;

    public string BucketName => _options.BucketName;

    public CouchbaseDataManager(
        ICluster cluster,
        CouchbaseStorageOptions options,
        IGrainStateSerializer serializer,
        ILogger<CouchbaseDataManager> logger)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 配置重试策略
        _retryPolicy = Policy
            .Handle<CouchbaseException>(IsTransient)
            .WaitAndRetryAsync(
                retryCount: _options.MaxRetries ?? 3,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * 100),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Retry {RetryCount} after {Delay}ms due to transient error",
                        retryCount,
                        timeSpan.TotalMilliseconds);
                });
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

    public async Task<(ReadOnlyMemory<byte> Data, ulong Cas)> ReadAsync(
        string grainType,
        string grainId,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var key = BuildDocumentKey(grainType, grainId);

        try
        {
            var result = await _retryPolicy.ExecuteAsync(async () =>
                await _collection!.GetAsync(key));

            if (result.ContentAs<StorageDocument>() is { } doc)
            {
                var data = Convert.FromBase64String(doc.Data);
                return (data, result.Cas);
            }

            return (ReadOnlyMemory<byte>.Empty, 0);
        }
        catch (global::Couchbase.Core.Exceptions.KeyValue.DocumentNotFoundException)
        {
            return (ReadOnlyMemory<byte>.Empty, 0);
        }
    }

    public async Task<ulong> WriteAsync(
        string grainType,
        string grainId,
        ReadOnlyMemory<byte> data,
        ulong cas,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var key = BuildDocumentKey(grainType, grainId);
        var doc = new StorageDocument
        {
            Data = Convert.ToBase64String(data.ToArray()),
            ContentType = _serializer.ContentType,
            Version = 2
        };

        try
        {
            IMutationResult result;
            if (cas != 0)
            {
                // Use ReplaceAsync with CAS for optimistic concurrency control
                var replaceOptions = new ReplaceOptions().Cas(cas);
                result = await _retryPolicy.ExecuteAsync(async () =>
                    await _collection!.ReplaceAsync(key, doc, replaceOptions));
            }
            else
            {
                // Use UpsertAsync for new documents or when CAS is not required
                result = await _retryPolicy.ExecuteAsync(async () =>
                    await _collection!.UpsertAsync(key, doc));
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
                await _retryPolicy.ExecuteAsync(async () =>
                    await _collection!.RemoveAsync(key, options));
            }
            else
            {
                await _retryPolicy.ExecuteAsync(async () =>
                    await _collection!.RemoveAsync(key));
            }
        }
        catch (global::Couchbase.Core.Exceptions.KeyValue.DocumentNotFoundException)
        {
            // 文档不存在视为删除成功
        }
        catch (global::Couchbase.Core.Exceptions.CasMismatchException)
        {
            throw new InconsistentStateException(
                "ETag mismatch during delete",
                cas.ToString(),
                "unknown");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_cluster != null)
        {
            await _cluster.DisposeAsync();
        }
    }

    private static string BuildDocumentKey(string grainType, string grainId)
        => $"{grainType}:{grainId}";

    private void EnsureInitialized()
    {
        if (_collection == null)
        {
            throw new InvalidOperationException(
                "CouchbaseDataManager not initialized. Call InitializeAsync first.");
        }
    }

    private static bool IsTransient(CouchbaseException ex)
    {
        return ex is global::Couchbase.Core.Exceptions.TemporaryFailureException
            or global::Couchbase.Core.Exceptions.TimeoutException
            or global::Couchbase.Core.Exceptions.RequestCanceledException;
    }
}
