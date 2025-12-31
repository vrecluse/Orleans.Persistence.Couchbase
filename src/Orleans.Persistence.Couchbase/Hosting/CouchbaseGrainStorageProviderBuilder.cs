using Couchbase;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Hosting;
using Orleans.Persistence.Couchbase.Configuration;
using Orleans.Persistence.Couchbase.Hosting;
using Orleans.Persistence.Couchbase.Serialization;
using Orleans.Providers;

[assembly: RegisterProvider("CouchbaseCluster", "GrainStorage", "Silo", typeof(CouchbaseGrainStorageProviderBuilder))]

namespace Orleans.Hosting;

internal sealed class CouchbaseGrainStorageProviderBuilder : IProviderBuilder<ISiloBuilder>
{
    public void Configure(ISiloBuilder builder, string? name, IConfigurationSection configurationSection)
    {
        builder.AddCouchbaseGrainStorage(name!, (OptionsBuilder<CouchbaseStorageOptions> optionsBuilder) =>
        {
            optionsBuilder.Configure<IServiceProvider>((options, services) =>
            {
                var serviceKey = configurationSection["ServiceKey"];
                if (!string.IsNullOrEmpty(serviceKey))
                {
                    // Get a cluster instance by service key.
                    // This allows sharing a single ICluster instance across multiple storage providers.
                    var cluster = services.GetRequiredKeyedService<ICluster>(serviceKey);

                    // Note: When using a keyed ICluster, ConnectionString is still required
                    // for CouchbaseStorageOptions validation, but the cluster instance takes precedence.
                    // Set a placeholder if not provided in configuration.
                    if (string.IsNullOrEmpty(options.ConnectionString))
                    {
                        options.ConnectionString = "couchbase://localhost"; // Placeholder for validation
                    }
                }
                else
                {
                    // Construct cluster from connection string
                    var connectionName = configurationSection["ConnectionName"];
                    var connectionString = configurationSection["ConnectionString"];

                    if (!string.IsNullOrEmpty(connectionName) && string.IsNullOrEmpty(connectionString))
                    {
                        var rootConfiguration = services.GetRequiredService<IConfiguration>();
                        connectionString = rootConfiguration.GetConnectionString(connectionName);
                    }

                    if (!string.IsNullOrEmpty(connectionString))
                    {
                        options.ConnectionString = connectionString;
                    }
                }

                // Bind other configuration properties
                var bucketName = configurationSection["BucketName"];
                if (!string.IsNullOrEmpty(bucketName))
                {
                    options.BucketName = bucketName;
                }

                var scopeName = configurationSection["ScopeName"];
                if (!string.IsNullOrEmpty(scopeName))
                {
                    options.ScopeName = scopeName;
                }

                var collectionName = configurationSection["CollectionName"];
                if (!string.IsNullOrEmpty(collectionName))
                {
                    options.CollectionName = collectionName;
                }

                var username = configurationSection["Username"];
                if (!string.IsNullOrEmpty(username))
                {
                    options.Username = username;
                }

                var password = configurationSection["Password"];
                if (!string.IsNullOrEmpty(password))
                {
                    options.Password = password;
                }

                var dataFormat = configurationSection["DefaultDataFormat"];
                if (!string.IsNullOrEmpty(dataFormat) &&
                    Enum.TryParse<CouchbaseDataFormat>(dataFormat, ignoreCase: true, out var format))
                {
                    options.DefaultDataFormat = format;
                }

                var operationTimeout = configurationSection["OperationTimeout"];
                if (!string.IsNullOrEmpty(operationTimeout) &&
                    TimeSpan.TryParse(operationTimeout, out var timeout))
                {
                    options.OperationTimeout = timeout;
                }

                var enableTracing = configurationSection["EnableTracing"];
                if (!string.IsNullOrEmpty(enableTracing) &&
                    bool.TryParse(enableTracing, out var tracing))
                {
                    options.EnableTracing = tracing;
                }

                var enableHealthCheck = configurationSection["EnableHealthCheck"];
                if (!string.IsNullOrEmpty(enableHealthCheck) &&
                    bool.TryParse(enableHealthCheck, out var healthCheck))
                {
                    options.EnableHealthCheck = healthCheck;
                }
            });
        });
    }
}
