using System.Diagnostics;

using Shardis.Diagnostics;
using Shardis.Hashing;
using Shardis.Model;
using Shardis.Persistence;
using Shardis.Routing;

namespace Shardis.Tests;

public class RouterTracingTests
{
    [Fact]
    public void Route_Starts_ShardisRoute_Activity()
    {
        var started = 0;
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == ShardisDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => { if (a.OperationName == "shardis.route") started++; },
            ActivityStopped = _ => { }
        };
        ActivitySource.AddActivityListener(listener);

        var shards = new List<IShard<string>>
        {
            new TestHelpers.TestShard<string>("s1", "c1")
        };

        var store = new InMemoryShardMapStore<string>();
        var router = new DefaultShardRouter<string, string>(store, shards, StringShardKeyHasher.Instance);
        var _ = router.RouteToShard(new ShardKey<string>("k1"));

        started.Should().BeGreaterThanOrEqualTo(1, $"Expected at least one 'shardis.route' activity start, saw {started}");
    }
}