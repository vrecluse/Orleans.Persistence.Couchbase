using Couchbase;
using Couchbase.Core.IO.Transcoders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Persistence.Couchbase.Configuration;
using Orleans.Persistence.Couchbase.Core;
using Orleans.Persistence.Couchbase.Infrastructure;
using Orleans.Storage;

namespace Orleans.Persistence.Couchbase.Hosting;

/// <summary>
/// IServiceCollection extension methods for Couchbase grain storage.
/// </summary>
public static class CouchbaseServiceCollectionExtensions
{
    /// <summary>
    /// Adds Couchbase grain storage with configuration delegate.
    /// </summary>
    public static IServiceCollection AddCouchbaseGrainStorage(
        this IServiceCollection services,
        string name,
        Action<CouchbaseStorageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(configure);

        // Configure options
        services.AddOptions<CouchbaseStorageOptions>(name)
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterCouchbaseServices(services, name);

        return services;
    }

    /// <summary>
    /// Adds Couchbase grain storage from IConfiguration binding.
    /// </summary>
    public static IServiceCollection AddCouchbaseGrainStorage(
        this IServiceCollection services,
        string name,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(configuration);

        // Bind configuration
        services.AddOptions<CouchbaseStorageOptions>(name)
            .Bind(configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterCouchbaseServices(services, name);

        return services;
    }

    private static void RegisterCouchbaseServices(IServiceCollection services, string name)
    {
        // Register ICluster as singleton (if not already registered)
        // Note: ICluster lifecycle is managed by DI container
        if (!services.Any(d => d.ServiceType == typeof(ICluster)))
        {
            services.AddSingleton<ICluster>(sp =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<CouchbaseStorageOptions>>()
                    .Get(name);

                var clusterOptions = new ClusterOptions
                {
                    ConnectionString = options.ConnectionString
                };

                if (!string.IsNullOrEmpty(options.Username) && !string.IsNullOrEmpty(options.Password))
                {
                    clusterOptions.UserName = options.Username;
                    clusterOptions.Password = options.Password;
                }

                if (options.OperationTimeout.HasValue)
                {
                    clusterOptions.KvTimeout = options.OperationTimeout.Value;
                }

                return Cluster.ConnectAsync(clusterOptions).GetAwaiter().GetResult();
            });
        }

        // Register high-performance transcoder
        services.TryAddSingleton<ITypeTranscoder, OrleansCouchbaseTranscoder>();

        // Register data manager
        services.AddKeyedSingleton<ICouchbaseDataManager>(name, (sp, key) =>
        {
            var cluster = sp.GetRequiredService<ICluster>();
            var options = sp.GetRequiredService<IOptionsMonitor<CouchbaseStorageOptions>>().Get(name);
            var transcoder = sp.GetRequiredService<ITypeTranscoder>();
            var logger = sp.GetRequiredService<ILogger<CouchbaseDataManager>>();

            return new CouchbaseDataManager(cluster, options, transcoder, logger);
        });

        // Register grain storage
        services.AddKeyedSingleton<IGrainStorage>(name, (sp, key) =>
        {
            var dataManager = sp.GetRequiredKeyedService<ICouchbaseDataManager>(key);
            var logger = sp.GetRequiredService<ILogger<CouchbaseGrainStorage>>();

            return new CouchbaseGrainStorage(name, dataManager, logger);
        });
    }
}
