using System.Diagnostics.Metrics;

namespace Shardis.Instrumentation;

/// <summary>
/// Default production metrics implementation backed by <see cref="Meter"/> counters.
/// Register this with DI to emit routing metrics; otherwise the internal no-op implementation is used.
/// </summary>
public sealed class MetricShardisMetrics : IShardisMetrics
{
    private static readonly Meter Meter = new("Shardis", "1.0.0");
    private static readonly Counter<long> RouteHits = Meter.CreateCounter<long>("shardis.route.hits");
    private static readonly Counter<long> RouteMisses = Meter.CreateCounter<long>("shardis.route.misses");
    private static readonly Counter<long> ExistingAssignments = Meter.CreateCounter<long>("shardis.route.assignments.existing");
    private static readonly Counter<long> NewAssignments = Meter.CreateCounter<long>("shardis.route.assignments.new");

    /// <summary>
    /// Records a successful routing decision to a shard.
    /// </summary>
    /// <param name="router">The router implementation name.</param>
    /// <param name="shardId">The target shard identifier.</param>
    /// <param name="existingAssignment">True if the key had a prior assignment (hit); false if a new assignment was created.</param>
    public void RouteHit(string router, string shardId, bool existingAssignment)
    {
        RouteHits.Add(1, new KeyValuePair<string, object?>("router", router), new KeyValuePair<string, object?>("shard", shardId));
        if (existingAssignment)
        {
            ExistingAssignments.Add(1, new KeyValuePair<string, object?>("router", router));
        }
        else
        {
            NewAssignments.Add(1, new KeyValuePair<string, object?>("router", router));
        }
    }

    /// <summary>
    /// Records a routing miss that resulted in a new shard assignment.
    /// </summary>
    /// <param name="router">The router implementation name.</param>
    public void RouteMiss(string router)
    {
        RouteMisses.Add(1, new KeyValuePair<string, object?>("router", router));
    }
}