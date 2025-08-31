namespace Shardis.Instrumentation;

/// <summary>
/// Contract for recording routing metrics and related diagnostic counters.
/// Implementations must be thread-safe.
/// </summary>
public interface IShardisMetrics
{
    /// <summary>
    /// Records a successful route decision.
    /// </summary>
    /// <param name="router">Router type name.</param>
    /// <param name="shardId">Target shard identifier.</param>
    /// <param name="existingAssignment">True if key already had an assignment; false if newly assigned.</param>
    void RouteHit(string router, string shardId, bool existingAssignment);

    /// <summary>
    /// Records a route miss (no prior assignment existed before hashing/selecting a shard).
    /// </summary>
    /// <param name="router">Router type name.</param>
    void RouteMiss(string router);

    /// <summary>
    /// Records routing latency in milliseconds (histogram recommended).
    /// </summary>
    /// <param name="elapsedMs">Latency in milliseconds.</param>
    void RecordRouteLatency(double elapsedMs);
}