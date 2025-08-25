using System.Diagnostics.Metrics;

namespace Shardis.Instrumentation;

/// <summary>
/// Metrics implementation backed by System.Diagnostics.Metrics.
/// </summary>
/// <summary>
/// Metrics implementation backed by <see cref="System.Diagnostics.Metrics"/> counters.
/// </summary>
/// <summary>
/// Default production metrics implementation backed by <see cref="System.Diagnostics.Metrics"/> counters.
/// Register this with DI to emit routing metrics; otherwise the internal no-op implementation is used.
/// </summary>
public sealed class MetricShardisMetrics : IShardisMetrics
{
    private static readonly Meter Meter = new("Shardis", "1.0.0");
    private static readonly Counter<long> RouteHits = Meter.CreateCounter<long>("shardis.route.hits");
    private static readonly Counter<long> RouteMisses = Meter.CreateCounter<long>("shardis.route.misses");
    private static readonly Counter<long> ExistingAssignments = Meter.CreateCounter<long>("shardis.route.assignments.existing");
    private static readonly Counter<long> NewAssignments = Meter.CreateCounter<long>("shardis.route.assignments.new");

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

    public void RouteMiss(string router)
    {
        RouteMisses.Add(1, new KeyValuePair<string, object?>("router", router));
    }
}