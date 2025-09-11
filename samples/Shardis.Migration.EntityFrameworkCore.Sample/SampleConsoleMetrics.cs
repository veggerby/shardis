using Shardis.Instrumentation;

namespace Shardis.Migration.EntityFrameworkCore.Sample;

/// <summary>
/// Minimal sample metrics implementation writing counters to the console. Not for production use.
/// </summary>
internal sealed class SampleConsoleMetrics : IShardisMetrics
{
    private long _hits;
    private long _misses;

    public void RouteHit(string router, string shardId, bool existingAssignment)
    {
        Interlocked.Increment(ref _hits);
        if ((_hits + _misses) % 1000 == 0)
        {
            Console.WriteLine($"[metrics] hits={_hits} misses={_misses}");
        }
    }

    public void RouteMiss(string router)
    {
        Interlocked.Increment(ref _misses);
    }

    public void RecordRouteLatency(double elapsedMs)
    {
        // Intentionally minimal; could bucket or average.
    }
}