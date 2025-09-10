using Shardis.Migration.Model;
using Shardis.Migration.Planning;
using Shardis.Model;
using Shardis.Persistence;

namespace Shardis.Migration.Tests;

public class SegmentedEnumerationMigrationPlannerTests
{
    private sealed class FakeEnumStore : IShardMapEnumerationStore<string>
    {
        private readonly List<ShardMap<string>> _items;
        private readonly Dictionary<ShardKey<string>, ShardId> _map;
        public FakeEnumStore(IEnumerable<ShardMap<string>> items)
        {
            _items = items.ToList();
            _map = _items.ToDictionary(i => i.ShardKey, i => i.ShardId);
        }
        public ShardMap<string> AssignShardToKey(ShardKey<string> shardKey, ShardId shardId)
        {
            _map[shardKey] = shardId;
            return new ShardMap<string>(shardKey, shardId);
        }
        public bool TryAssignShardToKey(ShardKey<string> shardKey, ShardId shardId, out ShardMap<string> shardMap)
        {
            if (_map.ContainsKey(shardKey))
            {
                shardMap = new ShardMap<string>(shardKey, _map[shardKey]);
                return false;
            }
            _map[shardKey] = shardId;
            shardMap = new ShardMap<string>(shardKey, shardId);
            return true;
        }
        public bool TryGetShardIdForKey(ShardKey<string> shardKey, out ShardId shardId) => _map.TryGetValue(shardKey, out shardId!);
        public bool TryGetOrAdd(ShardKey<string> shardKey, Func<ShardId> valueFactory, out ShardMap<string> shardMap)
        {
            if (_map.TryGetValue(shardKey, out var existing))
            {
                shardMap = new ShardMap<string>(shardKey, existing);
                return false;
            }
            var id = valueFactory();
            _map[shardKey] = id;
            shardMap = new ShardMap<string>(shardKey, id);
            return true;
        }
        public IAsyncEnumerable<ShardMap<string>> EnumerateAsync(CancellationToken cancellationToken = default) => EnumerateImpl();
        private async IAsyncEnumerable<ShardMap<string>> EnumerateImpl()
        {
            foreach (var i in _items)
            {
                yield return i;
                await Task.Yield();
            }
        }
    }

    [Fact]
    public async Task Planner_ComputesMoves_Segmented()
    {
        // arrange
        var keys = Enumerable.Range(0, 500).Select(i => new ShardMap<string>(new ShardKey<string>("k" + i), new ShardId("0"))).ToList();
        var store = new FakeEnumStore(keys);
        // Target: half keys to shard 1 (even indices), others remain shard 0
        var targetAssignments = keys.ToDictionary(k => k.ShardKey, k => (k.ShardKey.Value!.EndsWith("0") || int.Parse(k.ShardKey.Value![1..]) % 2 == 0) ? new ShardId("1") : k.ShardId);
        var target = new TopologySnapshot<string>(targetAssignments);
        var planner = new SegmentedEnumerationMigrationPlanner<string>(store, segmentSize: 100);

        // act
        var plan = await planner.CreatePlanAsync(new TopologySnapshot<string>(new Dictionary<ShardKey<string>, ShardId>()), target, CancellationToken.None);

        // assert
        plan.Moves.Should().NotBeEmpty();
        plan.Moves.Select(m => m.Target.Value).Should().Contain("1");
    }

    [Fact]
    public async Task DryRun_ReturnsCountsWithoutAllocatingMoves()
    {
        // arrange
        var keys = Enumerable.Range(0, 1000).Select(i => new ShardMap<string>(new ShardKey<string>("k" + i), new ShardId("0"))).ToList();
        var store = new FakeEnumStore(keys);
        var targetAssignments = keys.ToDictionary(k => k.ShardKey, k => (int.Parse(k.ShardKey.Value![1..]) % 3 == 0 ? new ShardId("1") : k.ShardId));
        var target = new TopologySnapshot<string>(targetAssignments);
        var planner = new SegmentedEnumerationMigrationPlanner<string>(store, segmentSize: 128);

        // act
        var (examined, moves) = await planner.DryRunAsync(target, CancellationToken.None);

        // assert
        examined.Should().Be(1000);
        moves.Should().BeGreaterThan(0);
        moves.Should().BeLessThan(1000);
    }
}