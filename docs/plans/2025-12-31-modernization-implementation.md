# Orleans.Persistence.Couchbase 现代化实施计划

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**目标：** 将 Orleans.Persistence.Couchbase 从 .NET Standard 2.0 / Orleans 3.0 升级到 .NET 10 / Orleans 9.x，增加 MessagePack 序列化支持，实现 Aspire 原生集成

**架构：** 二进制序列化优先设计，使用 Couchbase SDK 3.x 新 API（ICluster/IScope/ICollection），支持 IProviderBuilder 模式用于 .NET Aspire，xUnit v3 测试框架

**技术栈：** .NET 10, Orleans 9.2.1, CouchbaseNetClient 3.8.1, MessagePack 3.1.4, xUnit v3 3.2.1, Testcontainers 4.9.0

---

## 阶段 1：项目结构重组与依赖升级

### Task 1: 重组项目目录结构

**Files:**
- Move: `Orleans.Persistence.Couchbase/Orleans.Persistence.Couchbase.csproj` → `src/Orleans.Persistence.Couchbase/Orleans.Persistence.Couchbase.csproj`
- Move: All source files → `src/Orleans.Persistence.Couchbase/`
- Move: `Orleans.Persistence.Couchbase.UnitTests` → `tests/Orleans.Persistence.Couchbase.UnitTests`
- Move: `Orleans.Persistence.Couchbase.IntegrationTests` → `tests/Orleans.Persistence.Couchbase.IntegrationTests`
- Modify: `Orleans.Persistence.Couchbase.sln`

**Step 1: 创建新目录结构**

```bash
mkdir src
mkdir tests
```

**Step 2: 移动主项目**

```bash
mv Orleans.Persistence.Couchbase src/
```

**Step 3: 移动测试项目**

```bash
mv Orleans.Persistence.Couchbase.UnitTests tests/
mv Orleans.Persistence.Couchbase.IntegrationTests tests/
```

**Step 4: 更新解决方案文件引用**

打开 `Orleans.Persistence.Couchbase.sln`，更新项目路径：
- `Orleans.Persistence.Couchbase\Orleans.Persistence.Couchbase.csproj` → `src\Orleans.Persistence.Couchbase\Orleans.Persistence.Couchbase.csproj`
- `Orleans.Persistence.Couchbase.UnitTests\Orleans.Persistence.Couchbase.UnitTests.csproj` → `tests\Orleans.Persistence.Couchbase.UnitTests\Orleans.Persistence.Couchbase.UnitTests.csproj`
- `Orleans.Persistence.Couchbase.IntegrationTests\Orleans.Persistence.Couchbase.IntegrationTests.csproj` → `tests\Orleans.Persistence.Couchbase.IntegrationTests\Orleans.Persistence.Couchbase.IntegrationTests.csproj`

**Step 5: 验证项目加载**

```bash
dotnet restore
dotnet build
```

预期：所有项目成功构建

**Step 6: 提交更改**

```bash
git add .
git commit -m "refactor: reorganize project structure into src/ and tests/ directories

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

### Task 2: 升级主项目到 .NET 10 和 Orleans 9.x

**Files:**
- Modify: `src/Orleans.Persistence.Couchbase/Orleans.Persistence.Couchbase.csproj`

**Step 1: 更新目标框架和包版本**

修改 `Orleans.Persistence.Couchbase.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <PackageId>Orleans.Persistence.Couchbase</PackageId>
    <Version>2.0.0</Version>
    <Authors>Mark</Authors>
    <Company>MRD</Company>
    <Product>Orleans.Persistence.Couchbase</Product>
    <Description>Allows storing of Microsoft Orleans grain state in Couchbase with MessagePack support</Description>
    <RepositoryUrl>https://github.com/mrd1234/Orleans.Persistence.Couchbase</RepositoryUrl>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <PackageTags>Orleans Couchbase Storage Persistence StorageProvider MessagePack Aspire</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="CouchbaseNetClient" Version="3.8.1" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Options.DataAnnotations" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks" Version="10.0.0" />
    <PackageReference Include="Microsoft.Orleans.Core" Version="9.2.1" />
    <PackageReference Include="Microsoft.Orleans.Runtime" Version="9.2.1" />
    <PackageReference Include="MessagePack" Version="3.1.4" />
    <PackageReference Include="MessagePack.Annotations" Version="3.1.4" />
    <PackageReference Include="Polly" Version="8.5.0" />
    <PackageReference Include="System.Data.HashFunction.xxHash" Version="2.0.0" />
  </ItemGroup>

</Project>
```

**Step 2: 还原包**

```bash
cd src/Orleans.Persistence.Couchbase
dotnet restore
```

预期：所有包成功还原

**Step 3: 构建项目（预期失败）**

```bash
dotnet build
```

预期：编译错误（旧 API 不兼容）

**Step 4: 提交更改**

```bash
git add src/Orleans.Persistence.Couchbase/Orleans.Persistence.Couchbase.csproj
git commit -m "build: upgrade to .NET 10, Orleans 9.2.1, and Couchbase SDK 3.8.1

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

## 阶段 2：序列化层重新设计

### Task 3: 创建新的二进制序列化接口

**Files:**
- Create: `src/Orleans.Persistence.Couchbase/Serialization/IGrainStateSerializer.cs`
- Create: `src/Orleans.Persistence.Couchbase/Models/StorageDocument.cs`

**Step 1: 创建序列化接口**

`src/Orleans.Persistence.Couchbase/Serialization/IGrainStateSerializer.cs`:

```csharp
namespace Orleans.Persistence.Couchbase.Serialization;

/// <summary>
/// 粮食状态序列化器接口 - 二进制优先设计
/// </summary>
public interface IGrainStateSerializer
{
    /// <summary>
    /// 将粮食状态序列化为字节数组
    /// </summary>
    ReadOnlyMemory<byte> Serialize<T>(T grainState);

    /// <summary>
    /// 从字节数组反序列化粮食状态
    /// </summary>
    T Deserialize<T>(ReadOnlyMemory<byte> data);

    /// <summary>
    /// 序列化器的内容类型标识
    /// </summary>
    string ContentType { get; }
}
```

**Step 2: 创建存储文档模型**

`src/Orleans.Persistence.Couchbase/Models/StorageDocument.cs`:

```csharp
namespace Orleans.Persistence.Couchbase.Models;

/// <summary>
/// Couchbase 存储文档包装器
/// </summary>
public sealed class StorageDocument
{
    /// <summary>
    /// Base64 编码的序列化数据
    /// </summary>
    public required string Data { get; init; }

    /// <summary>
    /// 内容类型（如 application/json 或 application/x-msgpack）
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// 文档格式版本
    /// </summary>
    public int Version { get; init; } = 2;
}
```

**Step 3: 验证编译**

```bash
dotnet build src/Orleans.Persistence.Couchbase
```

预期：新文件编译成功

**Step 4: 提交更改**

```bash
git add src/Orleans.Persistence.Couchbase/Serialization/IGrainStateSerializer.cs
git add src/Orleans.Persistence.Couchbase/Models/StorageDocument.cs
git commit -m "feat: add binary-first IGrainStateSerializer interface and StorageDocument model

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

### Task 4: 实现 JSON 序列化器（System.Text.Json）

**Files:**
- Create: `src/Orleans.Persistence.Couchbase/Serialization/JsonGrainStateSerializer.cs`
- Delete: `src/Orleans.Persistence.Couchbase/Serialization/ISerializer.cs`
- Delete: `src/Orleans.Persistence.Couchbase/Serialization/JsonSerializer.cs`

**Step 1: 创建 JSON 序列化器实现**

`src/Orleans.Persistence.Couchbase/Serialization/JsonGrainStateSerializer.cs`:

```csharp
using System.Text;
using System.Text.Json;

namespace Orleans.Persistence.Couchbase.Serialization;

/// <summary>
/// 使用 System.Text.Json 的 JSON 序列化器
/// </summary>
public sealed class JsonGrainStateSerializer : IGrainStateSerializer
{
    private readonly JsonSerializerOptions _options;

    public string ContentType => "application/json; charset=utf-8";

    public JsonGrainStateSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public ReadOnlyMemory<byte> Serialize<T>(T grainState)
    {
        var json = JsonSerializer.Serialize(grainState, _options);
        return Encoding.UTF8.GetBytes(json);
    }

    public T Deserialize<T>(ReadOnlyMemory<byte> data)
    {
        var json = Encoding.UTF8.GetString(data.Span);
        return JsonSerializer.Deserialize<T>(json, _options)
            ?? throw new InvalidOperationException("Deserialization returned null");
    }
}
```

**Step 2: 删除旧的序列化器文件**

```bash
git rm src/Orleans.Persistence.Couchbase/Serialization/ISerializer.cs
git rm src/Orleans.Persistence.Couchbase/Serialization/JsonSerializer.cs
```

**Step 3: 验证编译**

```bash
dotnet build src/Orleans.Persistence.Couchbase
```

预期：编译错误（因为其他文件还引用旧接口）

**Step 4: 提交更改**

```bash
git add src/Orleans.Persistence.Couchbase/Serialization/
git commit -m "feat: implement JsonGrainStateSerializer using System.Text.Json

Replace old ISerializer with IGrainStateSerializer

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

### Task 5: 实现 MessagePack 序列化器

**Files:**
- Create: `src/Orleans.Persistence.Couchbase/Serialization/MessagePackGrainStateSerializer.cs`

**Step 1: 创建 MessagePack 序列化器实现**

`src/Orleans.Persistence.Couchbase/Serialization/MessagePackGrainStateSerializer.cs`:

```csharp
using MessagePack;

namespace Orleans.Persistence.Couchbase.Serialization;

/// <summary>
/// 使用 MessagePack 的二进制序列化器
/// </summary>
public sealed class MessagePackGrainStateSerializer : IGrainStateSerializer
{
    private readonly MessagePackSerializerOptions _options;

    public string ContentType => "application/x-msgpack";

    public MessagePackGrainStateSerializer(MessagePackSerializerOptions? options = null)
    {
        _options = options ?? MessagePackSerializerOptions.Standard;
    }

    public ReadOnlyMemory<byte> Serialize<T>(T grainState)
    {
        return MessagePackSerializer.Serialize(grainState, _options);
    }

    public T Deserialize<T>(ReadOnlyMemory<byte> data)
    {
        return MessagePackSerializer.Deserialize<T>(data, _options);
    }
}
```

**Step 2: 验证编译**

```bash
dotnet build src/Orleans.Persistence.Couchbase
```

预期：编译错误（其他文件引用问题）

**Step 3: 提交更改**

```bash
git add src/Orleans.Persistence.Couchbase/Serialization/MessagePackGrainStateSerializer.cs
git commit -m "feat: implement MessagePackGrainStateSerializer for binary serialization

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

## 阶段 3：Couchbase SDK 3.x 迁移

### Task 6: 重写 ICouchbaseDataManager 接口

**Files:**
- Modify: `src/Orleans.Persistence.Couchbase/Core/ICouchbaseDataManager.cs`
- Delete: `src/Orleans.Persistence.Couchbase/Models/ReadResponse.cs`

**Step 1: 更新接口定义**

`src/Orleans.Persistence.Couchbase/Core/ICouchbaseDataManager.cs`:

```csharp
namespace Orleans.Persistence.Couchbase.Core;

/// <summary>
/// Couchbase 数据管理器接口
/// </summary>
public interface ICouchbaseDataManager : IAsyncDisposable
{
    /// <summary>
    /// 初始化连接
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取粮食状态
    /// </summary>
    /// <param name="grainType">粮食类型</param>
    /// <param name="grainId">粮食ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据和 CAS 值的元组，如果不存在则返回空数据</returns>
    Task<(ReadOnlyMemory<byte> Data, ulong Cas)> ReadAsync(
        string grainType,
        string grainId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 写入粮食状态
    /// </summary>
    /// <param name="grainType">粮食类型</param>
    /// <param name="grainId">粮食ID</param>
    /// <param name="data">序列化数据</param>
    /// <param name="cas">CAS 值（0 表示新插入）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>新的 CAS 值</returns>
    Task<ulong> WriteAsync(
        string grainType,
        string grainId,
        ReadOnlyMemory<byte> data,
        ulong cas,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除粮食状态
    /// </summary>
    Task DeleteAsync(
        string grainType,
        string grainId,
        ulong cas,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 桶名称
    /// </summary>
    string BucketName { get; }
}
```

**Step 2: 删除旧的 ReadResponse 模型**

```bash
git rm src/Orleans.Persistence.Couchbase/Models/ReadResponse.cs
```

**Step 3: 提交更改**

```bash
git add src/Orleans.Persistence.Couchbase/Core/ICouchbaseDataManager.cs
git add src/Orleans.Persistence.Couchbase/Models/
git commit -m "refactor: update ICouchbaseDataManager for binary data and SDK 3.x

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

### Task 7: 重写 CouchbaseDataManager 使用 SDK 3.x API

**Files:**
- Modify: `src/Orleans.Persistence.Couchbase/Core/CouchbaseDataManager.cs`

**Step 1: 实现新的 CouchbaseDataManager**

`src/Orleans.Persistence.Couchbase/Core/CouchbaseDataManager.cs`:

```csharp
using Couchbase;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;
using Orleans.Persistence.Couchbase.Configuration;
using Orleans.Persistence.Couchbase.Exceptions;
using Orleans.Persistence.Couchbase.Models;
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
            .Handle<Couchbase.Core.Exceptions.CouchbaseException>(IsTransient)
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
                await _collection!.GetAsync(key, cancellationToken: cancellationToken));

            if (result.ContentAs<StorageDocument>() is { } doc)
            {
                var data = Convert.FromBase64String(doc.Data);
                return (data, result.Cas);
            }

            return (ReadOnlyMemory<byte>.Empty, 0);
        }
        catch (Couchbase.Core.Exceptions.DocumentNotFoundException)
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
            var options = new UpsertOptions();
            if (cas != 0)
            {
                options.Cas(cas);
            }

            var result = await _retryPolicy.ExecuteAsync(async () =>
                await _collection!.UpsertAsync(key, doc, options, cancellationToken));

            return result.Cas;
        }
        catch (Couchbase.Core.Exceptions.CasMismatchException ex)
        {
            throw new InconsistentStateException(
                "ETag mismatch - concurrent modification detected",
                cas.ToString(),
                ex.Context?.Cas.ToString() ?? "unknown");
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
            var options = new RemoveOptions();
            if (cas != 0)
            {
                options.Cas(cas);
            }

            await _retryPolicy.ExecuteAsync(async () =>
                await _collection!.RemoveAsync(key, options, cancellationToken));
        }
        catch (Couchbase.Core.Exceptions.DocumentNotFoundException)
        {
            // 文档不存在视为删除成功
        }
        catch (Couchbase.Core.Exceptions.CasMismatchException ex)
        {
            throw new InconsistentStateException(
                "ETag mismatch during delete",
                cas.ToString(),
                ex.Context?.Cas.ToString() ?? "unknown");
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

    private static bool IsTransient(Couchbase.Core.Exceptions.CouchbaseException ex)
    {
        return ex is Couchbase.Core.Exceptions.TemporaryFailureException
            or Couchbase.Core.Exceptions.TimeoutException
            or Couchbase.Core.Exceptions.RequestCanceledException;
    }
}
```

**Step 2: 验证编译**

```bash
dotnet build src/Orleans.Persistence.Couchbase
```

预期：编译错误（缺少 Configuration 命名空间）

**Step 3: 提交更改**

```bash
git add src/Orleans.Persistence.Couchbase/Core/CouchbaseDataManager.cs
git commit -m "refactor: rewrite CouchbaseDataManager using Couchbase SDK 3.x

Use ICluster, IScope, ICollection APIs
Add retry policy with Polly
Support binary data storage

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

## 阶段 4：配置与选项系统

### Task 8: 创建 CouchbaseStorageOptions

**Files:**
- Create: `src/Orleans.Persistence.Couchbase/Configuration/CouchbaseStorageOptions.cs`
- Create: `src/Orleans.Persistence.Couchbase/Configuration/SerializerType.cs`
- Delete: `src/Orleans.Persistence.Couchbase/Config/*` (旧配置文件)

**Step 1: 创建 SerializerType 枚举**

`src/Orleans.Persistence.Couchbase/Configuration/SerializerType.cs`:

```csharp
namespace Orleans.Persistence.Couchbase.Configuration;

/// <summary>
/// 序列化器类型
/// </summary>
public enum SerializerType
{
    /// <summary>
    /// JSON 序列化（System.Text.Json）
    /// </summary>
    Json,

    /// <summary>
    /// MessagePack 二进制序列化
    /// </summary>
    MessagePack
}
```

**Step 2: 创建配置选项类**

`src/Orleans.Persistence.Couchbase/Configuration/CouchbaseStorageOptions.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Orleans.Persistence.Couchbase.Configuration;

/// <summary>
/// Couchbase 存储配置选项
/// </summary>
public sealed class CouchbaseStorageOptions
{
    /// <summary>
    /// Couchbase 连接字符串（如 couchbase://localhost）
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 桶名称
    /// </summary>
    [Required]
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// 作用域名称（默认 _default）
    /// </summary>
    public string? ScopeName { get; set; }

    /// <summary>
    /// 集合名称（默认 _default）
    /// </summary>
    public string? CollectionName { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 密码
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 序列化器类型
    /// </summary>
    public SerializerType Serializer { get; set; } = SerializerType.Json;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int? MaxRetries { get; set; } = 3;

    /// <summary>
    /// 操作超时时间
    /// </summary>
    public TimeSpan? OperationTimeout { get; set; }

    /// <summary>
    /// 启用追踪
    /// </summary>
    public bool EnableTracing { get; set; } = false;

    /// <summary>
    /// 启用健康检查
    /// </summary>
    public bool EnableHealthCheck { get; set; } = true;
}
```

**Step 3: 删除旧配置文件**

```bash
git rm -r src/Orleans.Persistence.Couchbase/Config/
```

**Step 4: 提交更改**

```bash
git add src/Orleans.Persistence.Couchbase/Configuration/
git commit -m "feat: add CouchbaseStorageOptions with modern configuration system

Support DataAnnotations validation
Add SerializerType enum

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

## 阶段 5：Provider 注册与 Aspire 集成

### Task 9: 实现 IProviderBuilder

**Files:**
- Create: `src/Orleans.Persistence.Couchbase/Configuration/CouchbaseProviderBuilder.cs`
- Create: `src/Orleans.Persistence.Couchbase/Attributes/RegisterCouchbaseProviderAttribute.cs`

**Step 1: 创建 Provider Builder**

`src/Orleans.Persistence.Couchbase/Configuration/CouchbaseProviderBuilder.cs`:

```csharp
using Couchbase;
using Couchbase.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Orleans.Persistence.Couchbase.Core;
using Orleans.Persistence.Couchbase.Serialization;
using Orleans.Providers;
using Orleans.Storage;

namespace Orleans.Persistence.Couchbase.Configuration;

/// <summary>
/// Couchbase Provider Builder 实现
/// </summary>
public sealed class CouchbaseProviderBuilder : IProviderBuilder<ISiloBuilder>
{
    public string Name { get; }

    public CouchbaseProviderBuilder(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public void Build(ISiloBuilder builder, IConfiguration configuration)
    {
        var services = builder.GetServices();

        // 绑定配置
        var optionsSection = configuration.GetSection($"Orleans:Persistence:{Name}");
        services.AddOptions<CouchbaseStorageOptions>(Name)
            .Bind(optionsSection)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // 注册 Couchbase 集群（单例）
        services.AddCouchbase(clusterOptions =>
        {
            var options = optionsSection.Get<CouchbaseStorageOptions>()
                ?? throw new InvalidOperationException($"Configuration missing for {Name}");

            clusterOptions.ConnectionString = options.ConnectionString;
            if (!string.IsNullOrEmpty(options.Username) && !string.IsNullOrEmpty(options.Password))
            {
                clusterOptions.UserName = options.Username;
                clusterOptions.Password = options.Password;
            }
        });

        // 注册序列化器
        services.AddKeyedSingleton<IGrainStateSerializer>(Name, (sp, key) =>
        {
            var options = sp.GetRequiredKeyedService<IOptionsMonitor<CouchbaseStorageOptions>>(key)
                .Get(Name);

            return options.Serializer switch
            {
                SerializerType.Json => new JsonGrainStateSerializer(),
                SerializerType.MessagePack => new MessagePackGrainStateSerializer(),
                _ => throw new NotSupportedException($"Serializer type {options.Serializer} not supported")
            };
        });

        // 注册数据管理器
        services.AddKeyedSingleton<ICouchbaseDataManager>(Name, (sp, key) =>
        {
            var cluster = sp.GetRequiredService<ICluster>();
            var options = sp.GetRequiredKeyedService<IOptionsMonitor<CouchbaseStorageOptions>>(key)
                .Get(Name);
            var serializer = sp.GetRequiredKeyedService<IGrainStateSerializer>(key);
            var logger = sp.GetRequiredService<ILogger<CouchbaseDataManager>>();

            return new CouchbaseDataManager(cluster, options, serializer, logger);
        });

        // 注册 GrainStorage
        services.AddKeyedSingleton<IGrainStorage>(Name, (sp, key) =>
        {
            var dataManager = sp.GetRequiredKeyedService<ICouchbaseDataManager>(key);
            var serializer = sp.GetRequiredKeyedService<IGrainStateSerializer>(key);
            var logger = sp.GetRequiredService<ILogger<CouchbaseGrainStorage>>();

            return new CouchbaseGrainStorage(Name, dataManager, serializer, logger);
        });
    }
}
```

**Step 2: 创建 RegisterProvider 特性**

`src/Orleans.Persistence.Couchbase/Attributes/RegisterCouchbaseProviderAttribute.cs`:

```csharp
using Orleans.Persistence.Couchbase.Configuration;
using Orleans.Providers;

namespace Orleans.Persistence.Couchbase.Attributes;

/// <summary>
/// 注册 Couchbase 存储提供程序的特性
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class RegisterCouchbaseProviderAttribute : RegisterProviderAttribute
{
    /// <summary>
    /// 创建 Couchbase Provider 注册特性
    /// </summary>
    /// <param name="name">Provider 名称（默认 "Default"）</param>
    public RegisterCouchbaseProviderAttribute(string name = "Default")
        : base(typeof(CouchbaseProviderBuilder), name)
    {
    }
}
```

**Step 3: 验证编译**

```bash
dotnet build src/Orleans.Persistence.Couchbase
```

预期：编译错误（缺少扩展方法）

**Step 4: 提交更改**

```bash
git add src/Orleans.Persistence.Couchbase/Configuration/CouchbaseProviderBuilder.cs
git add src/Orleans.Persistence.Couchbase/Attributes/RegisterCouchbaseProviderAttribute.cs
git commit -m "feat: implement IProviderBuilder and RegisterProviderAttribute for Aspire

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

### Task 10: 创建 Aspire 友好的扩展方法

**Files:**
- Create: `src/Orleans.Persistence.Couchbase/Hosting/CouchbaseServiceCollectionExtensions.cs`
- Create: `src/Orleans.Persistence.Couchbase/Hosting/CouchbaseSiloBuilderExtensions.cs`

**Step 1: 创建 IServiceCollection 扩展**

`src/Orleans.Persistence.Couchbase/Hosting/CouchbaseServiceCollectionExtensions.cs`:

```csharp
using Couchbase;
using Couchbase.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            services.AddCouchbase(sp =>
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

                return clusterOptions;
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
```

**Step 2: 创建 ISiloBuilder 扩展**

`src/Orleans.Persistence.Couchbase/Hosting/CouchbaseSiloBuilderExtensions.cs`:

```csharp
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
```

**Step 3: 验证编译**

```bash
dotnet build src/Orleans.Persistence.Couchbase
```

预期：编译错误（CouchbaseGrainStorage 还未更新）

**Step 4: 提交更改**

```bash
git add src/Orleans.Persistence.Couchbase/Hosting/
git commit -m "feat: add Aspire-friendly extension methods for service registration

Support both Action<T> and IConfiguration binding

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

## 阶段 6：更新核心存储实现

### Task 11: 重写 CouchbaseGrainStorage

**Files:**
- Modify: `src/Orleans.Persistence.Couchbase/Core/CouchbaseGrainStorage.cs`

**Step 1: 更新 CouchbaseGrainStorage 实现**

`src/Orleans.Persistence.Couchbase/Core/CouchbaseGrainStorage.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Orleans.Persistence.Couchbase.Serialization;
using Orleans.Runtime;
using Orleans.Storage;

namespace Orleans.Persistence.Couchbase.Core;

/// <summary>
/// Couchbase Grain Storage 实现
/// </summary>
public sealed class CouchbaseGrainStorage : IGrainStorage
{
    private readonly string _name;
    private readonly ICouchbaseDataManager _dataManager;
    private readonly IGrainStateSerializer _serializer;
    private readonly ILogger<CouchbaseGrainStorage> _logger;

    public CouchbaseGrainStorage(
        string name,
        ICouchbaseDataManager dataManager,
        IGrainStateSerializer serializer,
        ILogger<CouchbaseGrainStorage> logger)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _dataManager = dataManager ?? throw new ArgumentNullException(nameof(dataManager));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ReadStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        try
        {
            var grainType = stateName;
            var grainIdString = grainId.ToString();

            var (data, cas) = await _dataManager.ReadAsync(grainType, grainIdString);

            if (data.Length > 0)
            {
                grainState.State = _serializer.Deserialize<T>(data);
                grainState.ETag = cas.ToString();
                grainState.RecordExists = true;
            }
            else
            {
                grainState.State = Activator.CreateInstance<T>();
                grainState.ETag = null;
                grainState.RecordExists = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading grain state for {GrainType}:{GrainId}", stateName, grainId);
            throw;
        }
    }

    public async Task WriteStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        try
        {
            var grainType = stateName;
            var grainIdString = grainId.ToString();

            var data = _serializer.Serialize(grainState.State);
            var cas = ulong.TryParse(grainState.ETag, out var parsedCas) ? parsedCas : 0;

            var newCas = await _dataManager.WriteAsync(grainType, grainIdString, data, cas);

            grainState.ETag = newCas.ToString();
            grainState.RecordExists = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing grain state for {GrainType}:{GrainId}", stateName, grainId);
            throw;
        }
    }

    public async Task ClearStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        try
        {
            var grainType = stateName;
            var grainIdString = grainId.ToString();

            var cas = ulong.TryParse(grainState.ETag, out var parsedCas) ? parsedCas : 0;

            await _dataManager.DeleteAsync(grainType, grainIdString, cas);

            grainState.State = Activator.CreateInstance<T>();
            grainState.ETag = null;
            grainState.RecordExists = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing grain state for {GrainType}:{GrainId}", stateName, grainId);
            throw;
        }
    }
}
```

**Step 2: 验证编译**

```bash
dotnet build src/Orleans.Persistence.Couchbase
```

预期：编译成功

**Step 3: 提交更改**

```bash
git add src/Orleans.Persistence.Couchbase/Core/CouchbaseGrainStorage.cs
git commit -m "refactor: update CouchbaseGrainStorage for Orleans 9.x and binary serialization

Use GrainId instead of GrainReference
Support IGrainStateSerializer

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

## 阶段 7：健康检查与异常处理

### Task 12: 创建健康检查实现

**Files:**
- Create: `src/Orleans.Persistence.Couchbase/Health/CouchbaseHealthCheck.cs`

**Step 1: 实现健康检查**

`src/Orleans.Persistence.Couchbase/Health/CouchbaseHealthCheck.cs`:

```csharp
using Couchbase;
using Couchbase.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Orleans.Persistence.Couchbase.Health;

/// <summary>
/// Couchbase 连接健康检查
/// </summary>
public sealed class CouchbaseHealthCheck : IHealthCheck
{
    private readonly ICluster _cluster;

    public CouchbaseHealthCheck(ICluster cluster)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var diagnostics = await _cluster.DiagnosticsAsync();

            var isHealthy = diagnostics.State == DiagnosticsState.Ok;

            var data = new Dictionary<string, object>
            {
                ["State"] = diagnostics.State.ToString(),
                ["Id"] = diagnostics.Id
            };

            return isHealthy
                ? HealthCheckResult.Healthy("Couchbase cluster is healthy", data)
                : HealthCheckResult.Degraded($"Couchbase cluster state: {diagnostics.State}", null, data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cannot connect to Couchbase cluster", ex);
        }
    }
}
```

**Step 2: 验证编译**

```bash
dotnet build src/Orleans.Persistence.Couchbase
```

预期：编译成功

**Step 3: 提交更改**

```bash
git add src/Orleans.Persistence.Couchbase/Health/CouchbaseHealthCheck.cs
git commit -m "feat: add CouchbaseHealthCheck for ASP.NET Core health checks

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

### Task 13: 完善异常层次

**Files:**
- Modify: `src/Orleans.Persistence.Couchbase/Exceptions/BucketConfigMissingFromConfigurationException.cs`
- Modify: `src/Orleans.Persistence.Couchbase/Exceptions/InvalidCouchbaseConfigurationException.cs`
- Modify: `src/Orleans.Persistence.Couchbase/Exceptions/UnableToDeterminePrimaryKeyException.cs`
- Create: `src/Orleans.Persistence.Couchbase/Exceptions/CouchbasePersistenceException.cs`

**Step 1: 创建基础异常类**

`src/Orleans.Persistence.Couchbase/Exceptions/CouchbasePersistenceException.cs`:

```csharp
namespace Orleans.Persistence.Couchbase.Exceptions;

/// <summary>
/// Couchbase 持久化异常基类
/// </summary>
public class CouchbasePersistenceException : Exception
{
    public CouchbasePersistenceException() { }

    public CouchbasePersistenceException(string message) : base(message) { }

    public CouchbasePersistenceException(string message, Exception innerException)
        : base(message, innerException) { }
}
```

**Step 2: 更新现有异常类**

更新 `BucketConfigMissingFromConfigurationException.cs`、`InvalidCouchbaseConfigurationException.cs`、`UnableToDeterminePrimaryKeyException.cs` 继承自 `CouchbasePersistenceException`。

**Step 3: 验证编译**

```bash
dotnet build src/Orleans.Persistence.Couchbase
```

预期：编译成功

**Step 4: 提交更改**

```bash
git add src/Orleans.Persistence.Couchbase/Exceptions/
git commit -m "refactor: add CouchbasePersistenceException base class

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

## 阶段 8：测试迁移到 xUnit v3

### Task 14: 升级测试项目到 .NET 10 和 xUnit v3

**Files:**
- Modify: `tests/Orleans.Persistence.Couchbase.UnitTests/Orleans.Persistence.Couchbase.UnitTests.csproj`
- Modify: `tests/Orleans.Persistence.Couchbase.IntegrationTests/Orleans.Persistence.Couchbase.IntegrationTests.csproj`

**Step 1: 更新单元测试项目文件**

`tests/Orleans.Persistence.Couchbase.UnitTests/Orleans.Persistence.Couchbase.UnitTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="3.2.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" Version="7.0.0" />
    <PackageReference Include="NSubstitute" Version="6.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Orleans.Persistence.Couchbase\Orleans.Persistence.Couchbase.csproj" />
  </ItemGroup>

</Project>
```

**Step 2: 更新集成测试项目文件**

`tests/Orleans.Persistence.Couchbase.IntegrationTests/Orleans.Persistence.Couchbase.IntegrationTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="3.2.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="Microsoft.Orleans.TestingHost" Version="9.2.1" />
    <PackageReference Include="Testcontainers.Couchbase" Version="4.9.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Orleans.Persistence.Couchbase\Orleans.Persistence.Couchbase.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>
```

**Step 3: 还原包**

```bash
dotnet restore
```

预期：包成功还原

**Step 4: 提交更改**

```bash
git add tests/
git commit -m "build: upgrade test projects to .NET 10 and xUnit v3

Replace NUnit with xUnit v3.2.1
Add Testcontainers support
Use NSubstitute instead of Moq

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

### Task 15: 迁移单元测试到 xUnit v3

**Files:**
- Modify: `tests/Orleans.Persistence.Couchbase.UnitTests/CouchbaseGrainStorageTests.cs`

**Step 1: 重写单元测试**

`tests/Orleans.Persistence.Couchbase.UnitTests/CouchbaseGrainStorageTests.cs`:

```csharp
using FluentAssertions;
using NSubstitute;
using Orleans.Persistence.Couchbase.Core;
using Orleans.Persistence.Couchbase.Serialization;
using Orleans.Runtime;
using Orleans.Storage;
using Xunit;

namespace Orleans.Persistence.Couchbase.UnitTests;

public sealed class CouchbaseGrainStorageTests
{
    [Fact]
    public async Task ReadStateAsync_ShouldDeserializeData_WhenDocumentExists()
    {
        // Arrange
        var dataManager = Substitute.For<ICouchbaseDataManager>();
        var serializer = Substitute.For<IGrainStateSerializer>();
        var logger = Substitute.For<ILogger<CouchbaseGrainStorage>>();

        var testData = "test data"u8.ToArray();
        dataManager.ReadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((new ReadOnlyMemory<byte>(testData), 123ul));

        serializer.Deserialize<string>(Arg.Any<ReadOnlyMemory<byte>>())
            .Returns("deserialized");

        var storage = new CouchbaseGrainStorage("test", dataManager, serializer, logger);
        var grainState = new GrainState<string>();
        var grainId = GrainId.Create("TestGrain", "key1");

        // Act
        await storage.ReadStateAsync("TestGrain", grainId, grainState);

        // Assert
        grainState.State.Should().Be("deserialized");
        grainState.ETag.Should().Be("123");
        grainState.RecordExists.Should().BeTrue();
    }

    [Fact]
    public async Task ReadStateAsync_ShouldReturnEmptyState_WhenDocumentNotFound()
    {
        // Arrange
        var dataManager = Substitute.For<ICouchbaseDataManager>();
        var serializer = Substitute.For<IGrainStateSerializer>();
        var logger = Substitute.For<ILogger<CouchbaseGrainStorage>>();

        dataManager.ReadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ReadOnlyMemory<byte>.Empty, 0ul));

        var storage = new CouchbaseGrainStorage("test", dataManager, serializer, logger);
        var grainState = new GrainState<string>();
        var grainId = GrainId.Create("TestGrain", "key1");

        // Act
        await storage.ReadStateAsync("TestGrain", grainId, grainState);

        // Assert
        grainState.State.Should().BeNull();
        grainState.ETag.Should().BeNull();
        grainState.RecordExists.Should().BeFalse();
    }

    [Fact]
    public async Task WriteStateAsync_ShouldSerializeAndWrite_WithCorrectCas()
    {
        // Arrange
        var dataManager = Substitute.For<ICouchbaseDataManager>();
        var serializer = Substitute.For<IGrainStateSerializer>();
        var logger = Substitute.For<ILogger<CouchbaseGrainStorage>>();

        var serializedData = "serialized"u8.ToArray();
        serializer.Serialize(Arg.Any<string>())
            .Returns(new ReadOnlyMemory<byte>(serializedData));

        dataManager.WriteAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ReadOnlyMemory<byte>>(),
            Arg.Any<ulong>(),
            Arg.Any<CancellationToken>())
            .Returns(456ul);

        var storage = new CouchbaseGrainStorage("test", dataManager, serializer, logger);
        var grainState = new GrainState<string>
        {
            State = "test",
            ETag = "123"
        };
        var grainId = GrainId.Create("TestGrain", "key1");

        // Act
        await storage.WriteStateAsync("TestGrain", grainId, grainState);

        // Assert
        await dataManager.Received(1).WriteAsync(
            "TestGrain",
            Arg.Any<string>(),
            Arg.Is<ReadOnlyMemory<byte>>(m => m.Span.SequenceEqual(serializedData)),
            123ul,
            Arg.Any<CancellationToken>());

        grainState.ETag.Should().Be("456");
        grainState.RecordExists.Should().BeTrue();
    }

    [Fact]
    public async Task ClearStateAsync_ShouldCallDelete()
    {
        // Arrange
        var dataManager = Substitute.For<ICouchbaseDataManager>();
        var serializer = Substitute.For<IGrainStateSerializer>();
        var logger = Substitute.For<ILogger<CouchbaseGrainStorage>>();

        var storage = new CouchbaseGrainStorage("test", dataManager, serializer, logger);
        var grainState = new GrainState<string>
        {
            State = "test",
            ETag = "123"
        };
        var grainId = GrainId.Create("TestGrain", "key1");

        // Act
        await storage.ClearStateAsync("TestGrain", grainId, grainState);

        // Assert
        await dataManager.Received(1).DeleteAsync(
            "TestGrain",
            Arg.Any<string>(),
            123ul,
            Arg.Any<CancellationToken>());

        grainState.State.Should().BeNull();
        grainState.ETag.Should().BeNull();
        grainState.RecordExists.Should().BeFalse();
    }
}
```

**Step 2: 删除旧的测试文件**

```bash
git rm tests/Orleans.Persistence.Couchbase.UnitTests/ITestGrain.cs
git rm tests/Orleans.Persistence.Couchbase.UnitTests/TestGrain.cs
```

**Step 3: 运行测试（预期失败）**

```bash
dotnet test tests/Orleans.Persistence.Couchbase.UnitTests
```

预期：编译错误（缺少 using 语句等）

**Step 4: 提交更改**

```bash
git add tests/Orleans.Persistence.Couchbase.UnitTests/
git commit -m "test: migrate unit tests to xUnit v3

Replace Moq with NSubstitute
Update to Orleans 9.x GrainId API

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

### Task 16: 创建集成测试基础设施

**Files:**
- Create: `tests/Orleans.Persistence.Couchbase.IntegrationTests/Infrastructure/CouchbaseTestFixture.cs`
- Create: `tests/Orleans.Persistence.Couchbase.IntegrationTests/Infrastructure/OrleansTestFixture.cs`

**Step 1: 创建 Couchbase Testcontainer 固件**

`tests/Orleans.Persistence.Couchbase.IntegrationTests/Infrastructure/CouchbaseTestFixture.cs`:

```csharp
using Testcontainers.Couchbase;
using Xunit;

namespace Orleans.Persistence.Couchbase.IntegrationTests.Infrastructure;

public sealed class CouchbaseTestFixture : IAsyncLifetime
{
    private CouchbaseContainer? _container;

    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Container not started");

    public string Username => "Administrator";
    public string Password => "password";
    public string BucketName => "test-bucket";

    public async Task InitializeAsync()
    {
        _container = new CouchbaseBuilder()
            .WithImage("couchbase:enterprise-7.6.0")
            .WithBucket(new Testcontainers.Couchbase.BucketDefinition
            {
                Name = BucketName,
                Type = Testcontainers.Couchbase.BucketType.Couchbase,
                RamQuotaMB = 256
            })
            .WithUsername(Username)
            .WithPassword(Password)
            .Build();

        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }
}
```

**Step 2: 创建 Orleans 测试固件**

`tests/Orleans.Persistence.Couchbase.IntegrationTests/Infrastructure/OrleansTestFixture.cs`:

```csharp
using Orleans.Hosting;
using Orleans.Persistence.Couchbase.Configuration;
using Orleans.Persistence.Couchbase.Hosting;
using Orleans.TestingHost;
using Xunit;

namespace Orleans.Persistence.Couchbase.IntegrationTests.Infrastructure;

public sealed class OrleansTestFixture : IAsyncLifetime
{
    private readonly CouchbaseTestFixture _couchbaseFixture;

    public TestCluster? Cluster { get; private set; }

    public OrleansTestFixture(CouchbaseTestFixture couchbaseFixture)
    {
        _couchbaseFixture = couchbaseFixture;
    }

    public async Task InitializeAsync()
    {
        var builder = new TestClusterBuilder();

        builder.AddSiloBuilderConfigurator<SiloConfigurator>();
        builder.AddClientBuilderConfigurator<ClientConfigurator>();

        Cluster = builder.Build();
        await Cluster.DeployAsync();
    }

    public async Task DisposeAsync()
    {
        if (Cluster != null)
        {
            await Cluster.StopAllSilosAsync();
        }
    }

    private class SiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddCouchbaseGrainStorage("TestStorageProvider", options =>
            {
                // 配置将从集成测试注入
            });
        }
    }

    private class ClientConfigurator : IClientBuilderConfigurator
    {
        public void Configure(IClientBuilder clientBuilder)
        {
            // 客户端配置
        }
    }
}
```

**Step 3: 验证编译**

```bash
dotnet build tests/Orleans.Persistence.Couchbase.IntegrationTests
```

预期：编译成功或有小错误

**Step 4: 提交更改**

```bash
git add tests/Orleans.Persistence.Couchbase.IntegrationTests/Infrastructure/
git commit -m "test: add Testcontainers and Orleans test fixtures

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

### Task 17: 重写集成测试

**Files:**
- Create: `tests/Orleans.Persistence.Couchbase.IntegrationTests/EndToEnd/BasicPersistenceTests.cs`
- Delete: `tests/Orleans.Persistence.Couchbase.IntegrationTests/CouchbaseTests.cs`
- Delete: `tests/Orleans.Persistence.Couchbase.IntegrationTests/TestBase.cs`

**Step 1: 创建基础持久化测试**

`tests/Orleans.Persistence.Couchbase.IntegrationTests/EndToEnd/BasicPersistenceTests.cs`:

```csharp
using FluentAssertions;
using Orleans.Persistence.Couchbase.IntegrationTests.Infrastructure;
using Xunit;

namespace Orleans.Persistence.Couchbase.IntegrationTests.EndToEnd;

[Collection("Couchbase")]
public sealed class BasicPersistenceTests : IClassFixture<OrleansTestFixture>
{
    private readonly OrleansTestFixture _orleansFixture;

    public BasicPersistenceTests(OrleansTestFixture orleansFixture)
    {
        _orleansFixture = orleansFixture;
    }

    [Fact]
    public async Task NewGrain_ShouldHaveEmptyState()
    {
        // Arrange
        var grain = _orleansFixture.Cluster!.GrainFactory.GetGrain<ITestGrain>("test-1");

        // Act
        var state = await grain.GetStateAsync();

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public async Task WriteAndRead_ShouldPersistState()
    {
        // Arrange
        var grain = _orleansFixture.Cluster!.GrainFactory.GetGrain<ITestGrain>("test-2");
        var testData = new MockState { Value = "test-data", Number = 42 };

        // Act
        await grain.SaveStateAsync(testData);
        var readState = await grain.GetStateAsync();

        // Assert
        readState.Should().BeEquivalentTo(testData);
    }

    [Fact]
    public async Task ClearState_ShouldRemoveState()
    {
        // Arrange
        var grain = _orleansFixture.Cluster!.GrainFactory.GetGrain<ITestGrain>("test-3");
        var testData = new MockState { Value = "to-delete", Number = 99 };

        await grain.SaveStateAsync(testData);

        // Act
        await grain.ClearStateAsync();
        var state = await grain.GetStateAsync();

        // Assert
        state.Should().BeNull();
    }
}
```

**Step 2: 删除旧测试文件**

```bash
git rm tests/Orleans.Persistence.Couchbase.IntegrationTests/CouchbaseTests.cs
git rm tests/Orleans.Persistence.Couchbase.IntegrationTests/TestBase.cs
```

**Step 3: 提交更改**

```bash
git add tests/Orleans.Persistence.Couchbase.IntegrationTests/
git commit -m "test: rewrite integration tests using xUnit v3 and Testcontainers

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

## 阶段 9：序列化器测试

### Task 18: 创建序列化器单元测试

**Files:**
- Create: `tests/Orleans.Persistence.Couchbase.UnitTests/Serialization/JsonSerializerTests.cs`
- Create: `tests/Orleans.Persistence.Couchbase.UnitTests/Serialization/MessagePackSerializerTests.cs`

**Step 1: JSON 序列化器测试**

**Step 2: MessagePack 序列化器测试**

**Step 3: 序列化往返测试**

**Step 4: 运行测试**

```bash
dotnet test tests/Orleans.Persistence.Couchbase.UnitTests
```

预期：所有测试通过

**Step 5: 提交更改**

```bash
git add tests/Orleans.Persistence.Couchbase.UnitTests/Serialization/
git commit -m "test: add comprehensive serializer unit tests

Test JSON and MessagePack serializers
Verify round-trip serialization

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

## 阶段 10：文档与最终验证

### Task 19: 更新 README

**Files:**
- Modify: `README.md`

**Step 1: 更新 README 内容**

**Step 2: 提交更改**

```bash
git add README.md
git commit -m "docs: update README for v2.0 with .NET 10 and MessagePack support

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

### Task 20: 运行完整测试套件

**Step 1: 运行所有测试**

```bash
dotnet test
```

预期：所有测试通过

**Step 2: 生成 NuGet 包**

```bash
dotnet pack src/Orleans.Persistence.Couchbase -c Release
```

预期：包成功生成

**Step 3: 最终提交**

```bash
git add .
git commit -m "chore: finalize v2.0 modernization

- .NET 10 support
- Orleans 9.2.1
- Couchbase SDK 3.8.1
- MessagePack serialization
- Aspire integration
- xUnit v3 tests

Generated with [Claude Code](https://claude.ai/code)
via [Happy](https://happy.engineering)

Co-Authored-By: Claude <noreply@anthropic.com>
Co-Authored-By: Happy <yesreply@happy.engineering>"
```

---

## 完成检查清单

- [ ] 所有项目升级到 .NET 10
- [ ] Orleans 升级到 9.2.1
- [ ] Couchbase SDK 升级到 3.8.1
- [ ] JSON 序列化器（System.Text.Json）实现
- [ ] MessagePack 序列化器实现
- [ ] IProviderBuilder 实现
- [ ] RegisterProviderAttribute 支持
- [ ] Aspire 友好扩展方法
- [ ] 健康检查实现
- [ ] 所有测试迁移到 xUnit v3
- [ ] Testcontainers 集成
- [ ] 所有测试通过
- [ ] 文档更新
- [ ] NuGet 包生成成功

---

**参考资源：**
- [Orleans 9.x 文档](https://learn.microsoft.com/en-us/dotnet/orleans/)
- [Couchbase SDK 3.x 文档](https://docs.couchbase.com/dotnet-sdk/current/)
- [xUnit v3 文档](https://xunit.net/docs/getting-started/v3)
- [MessagePack 文档](https://github.com/MessagePack-CSharp/MessagePack-CSharp)
