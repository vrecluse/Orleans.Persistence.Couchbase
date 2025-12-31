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
            // Use PingAsync for active health check
            var pingResult = await _cluster.PingAsync();

            var data = new Dictionary<string, object>
            {
                ["Id"] = pingResult.Id,
                ["Version"] = pingResult.Version.ToString()
            };

            // Check service states from the ping result
            var hasHealthyServices = false;
            var hasDegradedServices = false;

            foreach (var serviceEntry in pingResult.Services)
            {
                foreach (var endpoint in serviceEntry.Value)
                {
                    if (endpoint.State == ServiceState.Ok)
                    {
                        hasHealthyServices = true;
                    }
                    else
                    {
                        hasDegradedServices = true;
                    }
                }
            }

            if (hasHealthyServices && !hasDegradedServices)
            {
                return HealthCheckResult.Healthy("Couchbase cluster is healthy", data);
            }
            else if (hasHealthyServices)
            {
                return HealthCheckResult.Degraded("Couchbase cluster has degraded services", null, data);
            }
            else
            {
                return HealthCheckResult.Unhealthy("Couchbase cluster has no healthy services", null, data);
            }
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cannot connect to Couchbase cluster", ex);
        }
    }
}
