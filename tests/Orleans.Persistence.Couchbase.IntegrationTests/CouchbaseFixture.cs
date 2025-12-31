using System;
using System.Threading.Tasks;
using Testcontainers.Couchbase;
using Xunit;

namespace Orleans.Persistence.Couchbase.IntegrationTests;

/// <summary>
/// Test fixture for Couchbase integration tests using Testcontainers
/// </summary>
public sealed class CouchbaseFixture : IAsyncLifetime
{
    private CouchbaseContainer? _container;

    public const string BucketName = "test-bucket";
    public const string Username = "Administrator";
    public const string Password = "password";

    public global::Couchbase.ICluster? Cluster { get; private set; }

    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Container not initialized");

    public async ValueTask InitializeAsync()
    {
        // Create and start Couchbase container
        _container = new CouchbaseBuilder()
            .WithImage("couchbase:community")
            .Build();

        await _container.StartAsync();

        // Wait for Couchbase to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(15));

        var connectionString = _container.GetConnectionString();

        const int maxRetries = 10;
        Exception? lastException = null;

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                // Create fresh ClusterOptions on each retry to avoid disposed state
                var options = new global::Couchbase.ClusterOptions
                {
                    UserName = Username,
                    Password = Password
                };

                Cluster = await global::Couchbase.Cluster.ConnectAsync(connectionString, options);

                // Wait for cluster to be ready
                await Cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(60));

                // Create bucket
                var bucketManager = Cluster.Buckets;

                try
                {
                    await bucketManager.CreateBucketAsync(new global::Couchbase.Management.Buckets.BucketSettings
                    {
                        Name = BucketName,
                        RamQuotaMB = 128,
                        BucketType = global::Couchbase.Management.Buckets.BucketType.Couchbase
                    });
                }
                catch (global::Couchbase.Management.Buckets.BucketExistsException)
                {
                    // Bucket already exists, that's fine
                }

                // Wait for bucket to be ready
                var bucket = await Cluster.BucketAsync(BucketName);
                await bucket.WaitUntilReadyAsync(TimeSpan.FromSeconds(60));

                return; // Success
            }
            catch (Exception ex)
            {
                lastException = ex;

                // Cleanup on failure - but don't dispose, just set to null
                // The GC will handle cleanup
                Cluster = null;

                if (i < maxRetries - 1)
                {
                    // Linear backoff: 3s, 6s, 9s, ...
                    await Task.Delay(TimeSpan.FromSeconds(3 * (i + 1)));
                }
            }
        }

        throw new InvalidOperationException(
            $"Failed to connect to Couchbase and create bucket after {maxRetries} retries. Connection string: {connectionString}",
            lastException);
    }

    public async ValueTask DisposeAsync()
    {
        if (Cluster != null)
        {
            await Cluster.DisposeAsync();
        }

        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
}
