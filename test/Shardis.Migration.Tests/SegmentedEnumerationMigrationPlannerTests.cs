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
    public async Task Planner_NoMoves_When_Target_Matches()
    {
        // arrange
        var keys = Enumerable.Range(0, 100).Select(i => new ShardMap<string>(new ShardKey<string>("k" + i), new ShardId("s" + (i % 3)))).ToList();
        var store = new FakeEnumStore(keys);
        var target = new TopologySnapshot<string>(keys.ToDictionary(k => k.ShardKey, k => k.ShardId));
        var planner = new SegmentedEnumerationMigrationPlanner<string>(store, segmentSize: 25);

        // act
        var plan = await planner.CreatePlanAsync(new TopologySnapshot<string>(new Dictionary<ShardKey<string>, ShardId>()), target, CancellationToken.None);

        // assert
        plan.Moves.Should().BeEmpty();
    }

    [Fact]
    public async Task Planner_Honors_Cancellation_Between_Segments()
    {
        // arrange
        var keys = Enumerable.Range(0, 500).Select(i => new ShardMap<string>(new ShardKey<string>("k" + i), new ShardId("0"))).ToList();
        var store = new FakeEnumStore(keys);
        var targetAssignments = keys.ToDictionary(k => k.ShardKey, k => new ShardId("1"));
        var target = new TopologySnapshot<string>(targetAssignments);
        var planner = new SegmentedEnumerationMigrationPlanner<string>(store, segmentSize: 50);
        using var cts = new CancellationTokenSource();
        // cancel after first segment likely processed
        cts.CancelAfter(1);

        // act
        Func<Task> act = () => planner.CreatePlanAsync(new TopologySnapshot<string>(new Dictionary<ShardKey<string>, ShardId>()), target, cts.Token);

        // assert
        await act.Should().ThrowAsync<OperationCanceledException>();
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

    [Fact]
    public async Task DryRun_Honors_Cancellation()
    {
        // arrange
        var keys = Enumerable.Range(0, 1000).Select(i => new ShardMap<string>(new ShardKey<string>("k" + i), new ShardId("0"))).ToList();
        var store = new FakeEnumStore(keys);
        var targetAssignments = keys.ToDictionary(k => k.ShardKey, k => new ShardId("1"));
        var target = new TopologySnapshot<string>(targetAssignments);
        var planner = new SegmentedEnumerationMigrationPlanner<string>(store, segmentSize: 64);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // act
        Func<Task> act = async () => { await planner.DryRunAsync(target, cts.Token); };

        // assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}