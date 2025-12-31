using System.ComponentModel.DataAnnotations;

namespace Orleans.Persistence.Couchbase.Configuration;

/// <summary>
/// Configuration options for Couchbase grain storage.
/// </summary>
public sealed class CouchbaseStorageOptions
{
    /// <summary>
    /// Couchbase connection string (e.g., couchbase://localhost).
    /// </summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Bucket name for grain state storage.
    /// </summary>
    [Required]
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// Scope name (default: _default).
    /// </summary>
    public string? ScopeName { get; set; }

    /// <summary>
    /// Collection name (default: _default).
    /// </summary>
    public string? CollectionName { get; set; }

    /// <summary>
    /// Couchbase username for authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Couchbase password for authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Operation timeout. If not set, uses Couchbase SDK defaults.
    /// </summary>
    public TimeSpan? OperationTimeout { get; set; }

    /// <summary>
    /// Enable OpenTelemetry tracing.
    /// </summary>
    public bool EnableTracing { get; set; } = false;

    /// <summary>
    /// Enable health check endpoint.
    /// </summary>
    public bool EnableHealthCheck { get; set; } = true;
}
