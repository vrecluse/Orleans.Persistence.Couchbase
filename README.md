# Orleans.Persistence.Couchbase

Orleans grain state persistence provider for Couchbase.

![Master branch build](https://github.com/mrd1234/Orleans.Persistence.Couchbase/workflows/Master%20branch%20build/badge.svg?branch=master)

## Features

- ✅ **Orleans 9.x Support** - Built for Microsoft Orleans 9.2.1
- ✅ **.NET 10** - Modern .NET with latest features
- ✅ **Couchbase SDK 3.x** - Using Couchbase .NET Client 3.8.1
- ✅ **Binary-First Serialization** - Efficient `ReadOnlyMemory<byte>` operations
- ✅ **Multiple Serializers** - JSON (System.Text.Json) and MessagePack
- ✅ **Optimistic Concurrency** - CAS (Compare-And-Set) support
- ✅ **.NET Aspire Integration** - Modern service registration patterns
- ✅ **Health Checks** - ASP.NET Core health check support
- ✅ **Retry Policies** - Built-in Polly retry for transient failures

## Installation

```bash
dotnet add package Orleans.Persistence.Couchbase
```

## Quick Start

### Basic Configuration

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.UseOrleans(siloBuilder =>
{
    siloBuilder.AddCouchbaseGrainStorage("Default", options =>
    {
        options.ConnectionString = "couchbase://localhost";
        options.Username = "Administrator";
        options.Password = "password";
        options.BucketName = "my-bucket";
        options.Serializer = SerializerType.Json; // or SerializerType.MessagePack
    });
});
```

### Using Grain State

```csharp
public interface IMyGrain : IGrainWithStringKey
{
    Task<string> GetName();
    Task SetName(string name);
}

[GenerateSerializer]
public class MyState
{
    [Id(0)]
    public string Name { get; set; } = string.Empty;

    [Id(1)]
    public int Counter { get; set; }
}

public class MyGrain : Grain, IMyGrain
{
    private readonly IPersistentState<MyState> _state;

    public MyGrain(
        [PersistentState("state", "Default")] IPersistentState<MyState> state)
    {
        _state = state;
    }

    public Task<string> GetName() => Task.FromResult(_state.State.Name);

    public async Task SetName(string name)
    {
        _state.State.Name = name;
        await _state.WriteStateAsync();
    }
}
```

## Configuration Options

### CouchbaseStorageOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ConnectionString` | `string` | **Required** | Couchbase connection string (e.g., `couchbase://localhost`) |
| `Username` | `string` | **Required** | Couchbase username |
| `Password` | `string` | **Required** | Couchbase password |
| `BucketName` | `string` | **Required** | Bucket name for storage |
| `ScopeName` | `string?` | `_default` | Couchbase scope name |
| `CollectionName` | `string?` | `_default` | Couchbase collection name |
| `Serializer` | `SerializerType` | `Json` | Serializer type (Json or MessagePack) |
| `MaxRetries` | `int?` | `3` | Maximum retry attempts for transient failures |
| `EnableHealthCheck` | `bool` | `true` | Enable ASP.NET Core health checks |

### Serialization Options

#### JSON Serializer (System.Text.Json)

```csharp
options.Serializer = SerializerType.Json;
// Uses System.Text.Json with camelCase naming
// Content-Type: application/json; charset=utf-8
```

#### MessagePack Serializer

```csharp
options.Serializer = SerializerType.MessagePack;
// Uses MessagePack for compact binary serialization
// Content-Type: application/x-msgpack
// ~30-50% smaller than JSON
```

**Note:** MessagePack requires `[MessagePackObject]` and `[Key]` attributes:

```csharp
[MessagePackObject]
[GenerateSerializer]
public class MyState
{
    [Key(0), Id(0)]
    public string Name { get; set; } = string.Empty;

    [Key(1), Id(1)]
    public int Value { get; set; }
}
```

## Advanced Configuration

### Named Storage Providers

```csharp
siloBuilder
    .AddCouchbaseGrainStorage("UserStorage", options =>
    {
        options.BucketName = "users";
        options.CollectionName = "profiles";
        options.Serializer = SerializerType.Json;
    })
    .AddCouchbaseGrainStorage("SessionStorage", options =>
    {
        options.BucketName = "sessions";
        options.Serializer = SerializerType.MessagePack; // Faster for sessions
    });
```

### Health Checks

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add Couchbase with health checks enabled
builder.Services.AddCouchbaseGrainStorage("Default", options =>
{
    options.ConnectionString = "couchbase://localhost";
    options.BucketName = "my-bucket";
    options.EnableHealthCheck = true; // Default is true
});

// Map health check endpoint
var app = builder.Build();
app.MapHealthChecks("/health");
```

### .NET Aspire Integration

```csharp
// In AppHost project
var couchbase = builder.AddCouchbase("couchbase")
    .WithBucket("my-bucket");

var orleans = builder.AddProject<Projects.OrleansService>("orleans")
    .WithReference(couchbase);

// In OrleansService
builder.AddCouchbaseGrainStorage("Default", options =>
{
    // Configuration from Aspire connection string
    options.BucketName = "my-bucket";
});
```

## Document Structure

Documents are stored in Couchbase with the following structure:

```json
{
  "data": "base64-encoded-serialized-state",
  "contentType": "application/json; charset=utf-8",
  "version": 2
}
```

**Document Key Format:** `{GrainType}:{GrainId}`

Example: `MyGrain:user-123`

## Error Handling

The provider includes automatic retry with exponential backoff for transient errors:

- `TemporaryFailureException`
- `TimeoutException`
- `RequestCanceledException`

```csharp
options.MaxRetries = 5; // Default: 3
// Retry delay: 2^retryCount * 100ms
// Attempt 1: 200ms
// Attempt 2: 400ms
// Attempt 3: 800ms
```

### Exception Hierarchy

```
CouchbasePersistenceException
├── BucketConfigMissingFromConfigurationException
├── InvalidCouchbaseConfigurationException
└── UnableToDeterminePrimaryKeyException

Orleans.Storage.InconsistentStateException (thrown on CAS mismatch)
```

## Migration from 1.x

### Breaking Changes

1. **Orleans Upgrade:** Orleans 3.x → 9.x
2. **SDK Upgrade:** Couchbase SDK 2.x → 3.x
3. **Framework:** .NET Standard 2.0 → .NET 10
4. **Serialization:** String-based → Binary (`ReadOnlyMemory<byte>`)
5. **Configuration:** Different configuration API

### Migration Steps

1. **Update Package:**
   ```bash
   dotnet remove package Orleans.Persistence.Couchbase
   dotnet add package Orleans.Persistence.Couchbase --version 2.0.0
   ```

2. **Update Configuration:**
   ```csharp
   // Old (1.x)
   siloBuilder.AddCouchbaseGrainStorage("Default", cfg =>
   {
       cfg.ConnectionString = connectionString;
   });

   // New (2.x)
   siloBuilder.AddCouchbaseGrainStorage("Default", options =>
   {
       options.ConnectionString = "couchbase://localhost";
       options.Username = "Administrator";
       options.Password = "password";
       options.BucketName = "my-bucket";
   });
   ```

3. **Data Compatibility:**
   - Old documents (version 1) are **not compatible**
   - Perform data migration or start fresh
   - New format uses Base64-encoded binary data

## Performance Tips

1. **Use MessagePack for High-Throughput Scenarios:**
   - 30-50% smaller payload size
   - Faster serialization/deserialization
   - Lower bandwidth usage

2. **Enable Connection Pooling:**
   - Couchbase SDK 3.x handles connection pooling automatically
   - No additional configuration needed

3. **Use Scopes and Collections:**
   ```csharp
   options.ScopeName = "production";
   options.CollectionName = "grain-states";
   // Better organization and performance
   ```

4. **Monitor Health Checks:**
   - Use `/health` endpoint for monitoring
   - Integrate with Kubernetes liveness/readiness probes

## Testing

The project includes comprehensive tests:

- **Unit Tests:** 16 tests using xUnit v3 and Moq
- **Integration Tests:** Using Testcontainers.Couchbase
- **Serializer Tests:** JSON and MessagePack validation

```bash
# Run all tests
dotnet test

# Run only unit tests
dotnet test tests/Orleans.Persistence.Couchbase.UnitTests

# Run only integration tests (requires Docker)
dotnet test tests/Orleans.Persistence.Couchbase.IntegrationTests
```

## Requirements

- **.NET 10** or later
- **Orleans 9.2.1** or later
- **Couchbase Server 7.x** or later
- **Couchbase .NET Client 3.8.1** or later

## License

This project is licensed under the MIT License.

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## Changelog

### Version 2.0.0

- ✨ Upgraded to .NET 10
- ✨ Upgraded to Orleans 9.2.1
- ✨ Upgraded to Couchbase SDK 3.8.1
- ✨ Added MessagePack serialization support
- ✨ Added ASP.NET Core health checks
- ✨ Added Polly retry policies
- ✨ Binary-first serialization architecture
- ✨ .NET Aspire integration patterns
- 🔧 Improved exception hierarchy
- 🔧 Modern configuration with DataAnnotations validation
- 🧪 Migrated tests to xUnit v3
- 🧪 Added Testcontainers for integration tests

## Related Links

- [Orleans Documentation](https://learn.microsoft.com/en-us/dotnet/orleans/)
- [Couchbase .NET SDK](https://docs.couchbase.com/dotnet-sdk/current/hello-world/overview.html)
- [MessagePack for C#](https://github.com/MessagePack-CSharp/MessagePack-CSharp)
