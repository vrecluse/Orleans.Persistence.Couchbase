using Couchbase;
using Couchbase.Management.Buckets;
using DotNet.Testcontainers.Containers;
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

    public ICluster? Cluster { get; private set; }

    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Container not initialized");

    public async ValueTask InitializeAsync()
    {
        // Create and start Couchbase container
        _container = new CouchbaseBuilder()
            .WithImage("couchbase:community-7.6.4")
            .Build();

        await _container.StartAsync();

        // Connect to the cluster using default credentials
        // Testcontainers.Couchbase uses Administrator/password by default
        var options = new ClusterOptions
        {
            UserName = Username,
            Password = Password
        };

        Cluster = await global::Couchbase.Cluster.ConnectAsync(_container.GetConnectionString(), options);

        // Create bucket if needed
        try
        {
            var bucketManager = Cluster.Buckets;
            await bucketManager.CreateBucketAsync(new BucketSettings
            {
                Name = BucketName,
                RamQuotaMB = 100
            });

            var bucket = await Cluster.BucketAsync(BucketName);
            await bucket.WaitUntilReadyAsync(TimeSpan.FromSeconds(30));
        }
        catch
        {
            // Bucket might already exist
        }
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
