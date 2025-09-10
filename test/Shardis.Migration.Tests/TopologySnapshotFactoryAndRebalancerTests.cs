using Shardis.Migration.Model;
using Shardis.Migration.Planning;
using Shardis.Migration.Topology;
using Shardis.Model;
using Shardis.Persistence;

namespace Shardis.Migration.Tests;

public class TopologySnapshotFactoryAndRebalancerTests
{
    private sealed class EnumStore : IShardMapEnumerationStore<int>
    {
        private readonly List<ShardMap<int>> _items;
        public EnumStore(IEnumerable<ShardMap<int>> items) => _items = items.ToList();
        public ShardMap<int> AssignShardToKey(ShardKey<int> shardKey, ShardId shardId) => new(shardKey, shardId);
        public bool TryAssignShardToKey(ShardKey<int> shardKey, ShardId shardId, out ShardMap<int> shardMap) { shardMap = new(shardKey, shardId); return true; }
        public bool TryGetShardIdForKey(ShardKey<int> shardKey, out ShardId shardId) { shardId = new("0"); return true; }
        public bool TryGetOrAdd(ShardKey<int> shardKey, Func<ShardId> valueFactory, out ShardMap<int> shardMap) { shardMap = new(shardKey, valueFactory()); return true; }
        public async IAsyncEnumerable<ShardMap<int>> EnumerateAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var i in _items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return i; await Task.Yield();
            }
        }
    }

    [Fact]
    public async Task Snapshot_Fails_When_Max_Exceeded()
    {
        var items = Enumerable.Range(0, 3).Select(i => new ShardMap<int>(new ShardKey<int>(i), new ShardId("0")));
        var store = new EnumStore(items);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ToSnapshotAsync(maxKeys: 2));
    }

    [Fact]
    public async Task Snapshot_Succeeds_Within_Limit()
    {
        var items = Enumerable.Range(0, 5).Select(i => new ShardMap<int>(new ShardKey<int>(i), new ShardId("s" + (i % 2))));
        var store = new EnumStore(items);
        var snapshot = await store.ToSnapshotAsync(maxKeys: 10);
        snapshot.Assignments.Count.Should().Be(5);
    }

    [Fact]
    public void Rebalance_All_Changes()
    {
        var dict = Enumerable.Range(0, 4).ToDictionary(i => new ShardKey<int>(i), i => new ShardId("0"));
        var from = new TopologySnapshot<int>(dict);
        var target = TopologyRebalancer.Rebalance(from, k => new ShardId("1"));
        target.Assignments.Values.Select(v => v.Value).Distinct().Single().Should().Be("1");
    }

    [Fact]
    public void Rebalance_OnlyChanges()
    {
        var dict = Enumerable.Range(0, 4).ToDictionary(i => new ShardKey<int>(i), i => new ShardId(i % 2 == 0 ? "0" : "1"));
        var from = new TopologySnapshot<int>(dict);
        var reb = TopologyRebalancer.Rebalance(from, k => new ShardId("0"), onlyChanges: true);
        // only keys previously on shard "1" are moved to "0"; onlyChanges returns only changed assignments now pointing to "0"
        reb.Assignments.Count.Should().Be(2);
        reb.Assignments.Values.Select(v => v.Value).Distinct().Single().Should().Be("0");
    }

    [Fact]
    public void RebalanceWithHash_Empty_Shards_Throws()
    {
        var from = new TopologySnapshot<int>(new Dictionary<ShardKey<int>, ShardId>());
        Action act = () => TopologyRebalancer.RebalanceWithHash(from, Array.Empty<ShardId>(), k => 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}