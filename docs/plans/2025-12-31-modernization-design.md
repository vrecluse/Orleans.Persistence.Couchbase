# Orleans.Persistence.Couchbase 现代化设计方案

**日期：** 2025-12-31
**状态：** 已批准

## 概述

将 Orleans.Persistence.Couchbase 项目从 .NET Standard 2.0 / Orleans 3.0 现代化升级到 .NET 10 / Orleans 9.x，增加 MessagePack 序列化支持，实现 Aspire 原生集成。

## 目标版本

| 组件 | 当前版本 | 目标版本 |
|------|----------|----------|
| .NET | .NET Standard 2.0 / .NET Core 3.0 | .NET 10.0 |
| Orleans | 3.0.0 | 9.x (最新稳定版) |
| Couchbase SDK | 2.7.12 | 最新可用版本 |
| 测试框架 | NUnit 3.12 | xUnit v3 |
| JSON 序列化 | Newtonsoft.Json | System.Text.Json |
| 新增 | - | MessagePack-CSharp |

## 项目结构

```
Orleans.Persistence.Couchbase/
├── Orleans.Persistence.Couchbase.sln
├── src/
│   └── Orleans.Persistence.Couchbase/
│       ├── Orleans.Persistence.Couchbase.csproj
│       ├── Core/
│       │   ├── CouchbaseGrainStorage.cs
│       │   ├── CouchbaseDataManager.cs
│       │   └── ICouchbaseDataManager.cs
│       ├── Serialization/
│       │   ├── IGrainStateSerializer.cs
│       │   ├── JsonGrainStateSerializer.cs
│       │   └── MessagePackGrainStateSerializer.cs
│       ├── Configuration/
│       │   ├── CouchbaseStorageOptions.cs
│       │   ├── CouchbaseStorageOptionsValidator.cs
│       │   └── CouchbaseProviderBuilder.cs
│       ├── Hosting/
│       │   ├── CouchbaseSiloBuilderExtensions.cs
│       │   └── CouchbaseServiceCollectionExtensions.cs
│       ├── Attributes/
│       │   └── RegisterCouchbaseProviderAttribute.cs
│       ├── Exceptions/
│       │   └── CouchbasePersistenceException.cs
│       └── Health/
│           └── CouchbaseHealthCheck.cs
├── tests/
│   ├── Orleans.Persistence.Couchbase.UnitTests/
│   └── Orleans.Persistence.Couchbase.IntegrationTests/
└── docs/
```

## 序列化架构

### 新接口设计（破坏性变更）

```csharp
public interface IGrainStateSerializer
{
    ReadOnlyMemory<byte> Serialize<T>(T grainState);
    T Deserialize<T>(ReadOnlyMemory<byte> data);
    string ContentType { get; }
}
```

### 存储文档格式

```json
{
  "_contentType": "application/x-msgpack",
  "_data": "<base64-encoded-bytes>",
  "_version": 2
}
```

### 实现类

- **JsonGrainStateSerializer**: 使用 System.Text.Json，ContentType: `application/json; charset=utf-8`
- **MessagePackGrainStateSerializer**: 使用 MessagePack-CSharp，ContentType: `application/x-msgpack`

## Provider 注册与 Aspire 集成

### IProviderBuilder 实现

```csharp
public class CouchbaseProviderBuilder : IProviderBuilder<ISiloBuilder>
{
    public string Name { get; }

    public void Build(ISiloBuilder builder, IConfiguration configuration)
    {
        var options = new CouchbaseStorageOptions();
        configuration.GetSection($"Orleans:Persistence:{Name}").Bind(options);

        builder.Services.AddKeyedSingleton<IGrainStorage>(Name, ...);
    }
}
```

### RegisterProviderAttribute

```csharp
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class RegisterCouchbaseProviderAttribute : RegisterProviderAttribute
{
    public RegisterCouchbaseProviderAttribute(string name = "Default")
        : base(typeof(CouchbaseProviderBuilder), name) { }
}
```

### Aspire 扩展方法

```csharp
public static IServiceCollection AddCouchbaseGrainStorage(
    this IServiceCollection services,
    string name,
    Action<CouchbaseStorageOptions> configure);

public static IServiceCollection AddCouchbaseGrainStorage(
    this IServiceCollection services,
    string name,
    IConfiguration configuration);
```

### 配置示例

```json
{
  "Orleans": {
    "Persistence": {
      "Default": {
        "ConnectionString": "couchbase://localhost",
        "Username": "admin",
        "Password": "password",
        "BucketName": "orleans-grains",
        "ScopeName": "_default",
        "CollectionName": "_default",
        "Serializer": "MessagePack"
      }
    }
  }
}
```

## Couchbase SDK 3.x 迁移

### 主要变更

- 使用 `ICluster`、`IScope`、`ICollection` 层次结构
- `ClusterOptions` 替代旧的 `ClientConfiguration`
- 原生 async/await API
- 连接字符串配置模式

### CouchbaseDataManager 核心方法

```csharp
public async Task<(ReadOnlyMemory<byte> Data, ulong Cas)> ReadAsync(
    string grainType, string grainId);

public async Task<ulong> WriteAsync(
    string grainType, string grainId, ReadOnlyMemory<byte> data, ulong cas);

public async Task DeleteAsync(
    string grainType, string grainId, ulong cas);
```

### 文档键命名

```
格式: {grainType}:{grainId}
示例: UserGrain:user-12345
```

## 测试策略

### xUnit v3 迁移

| NUnit | xUnit |
|-------|-------|
| `[TestFixture]` | 删除 |
| `[Test]` | `[Fact]` |
| `[TestCase]` | `[Theory]` + `[InlineData]` |
| `[SetUp]` | 构造函数 |
| `[TearDown]` | `IDisposable.Dispose()` |
| `[OneTimeSetUp]` | `IAsyncLifetime.InitializeAsync()` |
| `[OneTimeTearDown]` | `IAsyncLifetime.DisposeAsync()` |

### 集成测试

- 使用 Testcontainers 启动 Couchbase
- Orleans TestCluster 集成测试
- 目标：>90% 代码覆盖率

## 错误处理与弹性

### 重试策略

使用 Polly 实现指数退避重试：
- 最大重试次数：3
- 退避策略：指数增长（100ms, 200ms, 400ms）
- 可重试异常：`TemporaryFailureException`, `TimeoutException`

### 健康检查

实现 `IHealthCheck` 接口，集成到 ASP.NET Core 健康检查系统。

## 配置选项

```csharp
public class CouchbaseStorageOptions
{
    [Required] public string ConnectionString { get; set; }
    [Required] public string BucketName { get; set; }
    public string ScopeName { get; set; } = "_default";
    public string CollectionName { get; set; } = "_default";
    public string Username { get; set; }
    public string Password { get; set; }
    public SerializerType Serializer { get; set; } = SerializerType.Json;
    public int? MaxRetries { get; set; } = 3;
    public TimeSpan? OperationTimeout { get; set; }
    public bool EnableTracing { get; set; } = false;
    public bool EnableHealthCheck { get; set; } = true;
}

public enum SerializerType { Json, MessagePack }
```

## 破坏性变更清单

1. **目标框架：** .NET Standard 2.0 → .NET 10.0
2. **Orleans：** 3.0 → 9.x
3. **Couchbase SDK：** 2.x → 3.x
4. **序列化接口：** `ISerializer` (string) → `IGrainStateSerializer` (binary)
5. **存储格式：** 直接 JSON → 包装的二进制格式
6. **配置方式：** `ISiloBuilder` 扩展 → `IServiceCollection` + `IProviderBuilder`
7. **JSON 库：** Newtonsoft.Json → System.Text.Json

## 迁移步骤

1. 升级项目到 .NET 10
2. 更新 NuGet 包引用
3. 修改配置代码
4. 数据迁移（可选）
5. 测试验证
