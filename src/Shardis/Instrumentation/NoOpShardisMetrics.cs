namespace Shardis.Instrumentation;

/// <summary>
/// No-op metrics implementation used by default when no instrumentation is configured.
/// </summary>
internal sealed class NoOpShardisMetrics : IShardisMetrics
{
    public static readonly IShardisMetrics Instance = new NoOpShardisMetrics();

    private NoOpShardisMetrics() { }

    public void RouteHit(string router, string shardId, bool existingAssignment) { }

    public void RouteMiss(string router) { }

    public void RecordRouteLatency(double elapsedMs) { }
}