using Couchbase;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Persistence.Couchbase.Configuration;
using Orleans.Persistence.Couchbase.Core;
using Orleans.Persistence.Couchbase.Serialization;
using Orleans.Storage;

namespace Orleans.Persistence.Couchbase.Hosting;

/// <summary>
/// Couchbase 存储的 IServiceCollection 扩展方法
/// </summary>
public static class CouchbaseServiceCollectionExtensions
{
    /// <summary>
    /// 添加 Couchbase Grain Storage（使用配置委托）
    /// </summary>
    public static IServiceCollection AddCouchbaseGrainStorage(
        this IServiceCollection services,
        string name,
        Action<CouchbaseStorageOptions> configure)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (name == null) throw new ArgumentNullException(nameof(name));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        // 配置选项
        services.AddOptions<CouchbaseStorageOptions>(name)
            .Configure(configure)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterCouchbaseServices(services, name);

        return services;
    }

    /// <summary>
    /// 添加 Couchbase Grain Storage（从 IConfiguration 绑定）
    /// </summary>
    public static IServiceCollection AddCouchbaseGrainStorage(
        this IServiceCollection services,
        string name,
        IConfiguration configuration)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (name == null) throw new ArgumentNullException(nameof(name));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        // 绑定配置
        services.AddOptions<CouchbaseStorageOptions>(name)
            .Bind(configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterCouchbaseServices(services, name);

        return services;
    }

    private static void RegisterCouchbaseServices(IServiceCollection services, string name)
    {
        // 注册 Couchbase 集群（如果尚未注册）
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

                return Cluster.ConnectAsync(clusterOptions).GetAwaiter().GetResult();
            });
        }

        // 注册序列化器
        services.AddKeyedSingleton<IGrainStateSerializer>(name, (sp, key) =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<CouchbaseStorageOptions>>()
                .Get(name);

            return options.Serializer switch
            {
                SerializerType.Json => new JsonGrainStateSerializer(),
                SerializerType.MessagePack => new MessagePackGrainStateSerializer(),
                _ => throw new NotSupportedException($"Serializer {options.Serializer} not supported")
            };
        });

        // 注册数据管理器
        services.AddKeyedSingleton<ICouchbaseDataManager>(name, (sp, key) =>
        {
            var cluster = sp.GetRequiredService<ICluster>();
            var options = sp.GetRequiredService<IOptionsMonitor<CouchbaseStorageOptions>>().Get(name);
            var serializer = sp.GetRequiredKeyedService<IGrainStateSerializer>(key);
            var logger = sp.GetRequiredService<ILogger<CouchbaseDataManager>>();

            return new CouchbaseDataManager(cluster, options, serializer, logger);
        });

        // 注册 GrainStorage
        services.AddKeyedSingleton<IGrainStorage>(name, (sp, key) =>
        {
            var dataManager = sp.GetRequiredKeyedService<ICouchbaseDataManager>(key);
            var serializer = sp.GetRequiredKeyedService<IGrainStateSerializer>(key);
            var logger = sp.GetRequiredService<ILogger<CouchbaseGrainStorage>>();

            return new CouchbaseGrainStorage(name, dataManager, serializer, logger);
        });
    }
}
