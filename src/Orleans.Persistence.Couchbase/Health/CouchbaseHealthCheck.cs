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
