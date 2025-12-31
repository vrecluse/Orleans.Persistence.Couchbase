using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Orleans.Persistence.Couchbase.Configuration;

namespace Orleans.Persistence.Couchbase.Hosting;

/// <summary>
/// ISiloBuilder 扩展方法
/// </summary>
public static class CouchbaseSiloBuilderExtensions
{
    /// <summary>
    /// 添加 Couchbase Grain Storage
    /// </summary>
    public static ISiloBuilder AddCouchbaseGrainStorage(
        this ISiloBuilder builder,
        string name,
        Action<CouchbaseStorageOptions> configure)
    {
        builder.ConfigureServices(services =>
            services.AddCouchbaseGrainStorage(name, configure));
        return builder;
    }

    /// <summary>
    /// 添加 Couchbase Grain Storage（从配置绑定）
    /// </summary>
    public static ISiloBuilder AddCouchbaseGrainStorage(
        this ISiloBuilder builder,
        string name,
        IConfiguration configuration)
    {
        builder.ConfigureServices(services =>
            services.AddCouchbaseGrainStorage(name, configuration));
        return builder;
    }
}
