using Microsoft.Extensions.DependencyInjection;

using Shardis.Logging;
using Shardis.Migration.Abstractions;
using Shardis.Migration.InMemory;
using Shardis.Migration.Model;
using Shardis.Migration.Planning;
using Shardis.Model;
using Shardis.Persistence;
using Shardis.Testing;

namespace Shardis.Migration.Tests;

public class AdditionalMigrationCoverageTests
{
    private sealed class EnumStore<T> : IShardMapEnumerationStore<T> where T : notnull, IEquatable<T>
    {
        private readonly List<ShardMap<T>> _items;
        public EnumStore(IEnumerable<ShardMap<T>> items) => _items = items.ToList();
        public ShardMap<T> AssignShardToKey(ShardKey<T> shardKey, ShardId shardId) => new(shardKey, shardId);
        public bool TryAssignShardToKey(ShardKey<T> shardKey, ShardId shardId, out ShardMap<T> shardMap) { shardMap = new(shardKey, shardId); return true; }
        public bool TryGetShardIdForKey(ShardKey<T> shardKey, out ShardId shardId) { shardId = new("0"); return true; }
        public bool TryGetOrAdd(ShardKey<T> shardKey, Func<ShardId> valueFactory, out ShardMap<T> shardMap) { shardMap = new(shardKey, valueFactory()); return true; }
        public async IAsyncEnumerable<ShardMap<T>> EnumerateAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var i in _items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return i; await Task.Yield();
            }
        }
    }

    [Fact]
    public void Rebalance_EmptySource_ReturnsEmpty()
    {
        // arrange
        var empty = new TopologySnapshot<string>(new Dictionary<ShardKey<string>, ShardId>());

        // act
        var result = TopologyRebalancer.Rebalance(empty, k => new ShardId("x"));

        // assert
        result.Assignments.Should().BeEmpty();
    }

    [Fact]
    public void RebalanceWithHash_OnlyChanges_NoDiff_EmptyResult()
    {
        // arrange
        var dict = Enumerable.Range(0, 10).ToDictionary(i => new ShardKey<int>(i), i => new ShardId("s" + (i % 2)));
        var from = new TopologySnapshot<int>(dict);
        var shards = new[] { new ShardId("s0"), new ShardId("s1") };
        static ulong Hash(ShardKey<int> k) => (ulong)k.Value!.GetHashCode();

        // act
        var reb = TopologyRebalancer.RebalanceWithHash(from, shards, Hash, onlyChanges: true);

        // assert
        reb.Assignments.Should().BeEmpty();
    }

    [Fact]
    public void ServiceCollection_AddShardisMigration_PreservesExistingPlanner()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddSingleton<IShardMigrationPlanner<string>, InMemoryMigrationPlanner<string>>();
        services.AddShardisMigration<string>();

        // act
        var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IShardMigrationPlanner<string>>();

        // assert
        resolved.Should().BeOfType<InMemoryMigrationPlanner<string>>();
    }


    [Fact]
    public async Task SegmentedPlanner_MoveOrdering_Deterministic()
    {
        // arrange
        var keys = Enumerable.Range(0, 50).Select(i => new ShardMap<string>(new ShardKey<string>("k" + i), new ShardId("s0"))).ToList();
        var store = new EnumStore<string>(keys);
        var targetAssignments = keys.ToDictionary(k => k.ShardKey, k => (int.Parse(k.ShardKey.Value![1..]) % 2 == 0 ? new ShardId("s1") : k.ShardId));
        var target = new TopologySnapshot<string>(targetAssignments);
        var planner = new SegmentedEnumerationMigrationPlanner<string>(store, segmentSize: 10);

        // act
        var plan1 = await planner.CreatePlanAsync(new TopologySnapshot<string>(new Dictionary<ShardKey<string>, ShardId>()), target, CancellationToken.None);
        var plan2 = await planner.CreatePlanAsync(new TopologySnapshot<string>(new Dictionary<ShardKey<string>, ShardId>()), target, CancellationToken.None);

        // assert
        plan1.Moves.Select(m => m.Key.Value).Should().Equal(plan2.Moves.Select(m => m.Key.Value));
    }

    [Fact]
    public async Task InMemoryShardMapStore_Enumeration_Can_Be_Canceled()
    {
        // arrange
        var store = new InMemoryShardMapStore<string>();
        for (int i = 0; i < 100; i++)
        {
            store.AssignShardToKey(new ShardKey<string>("k" + i), new ShardId("s" + (i % 2)));
        }
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // act
        Func<Task> act = async () => { await foreach (var _ in store.EnumerateAsync(cts.Token)) { } };

        // assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void InMemoryLogger_Respects_MinLevel()
    {
        // arrange
        var logger = new InMemoryShardisLogger(ShardisLogLevel.Warning);

        // act
        logger.Log(ShardisLogLevel.Information, "info");
        logger.Log(ShardisLogLevel.Error, "error");

        // assert
        logger.Entries.Any(e => e.Contains("info")).Should().BeFalse();
        logger.Entries.Any(e => e.Contains("error")).Should().BeTrue();
    }

    [Fact]
    public void Determinism_MakeDelays_SkewProfiles()
    {
        // arrange
        var det = Determinism.Create(123);
        var baseDelay = TimeSpan.FromMilliseconds(5);

        // act
        var mild = det.MakeDelays(4, Skew.Mild, baseDelay, steps: 4);
        var harsh = det.MakeDelays(4, Skew.Harsh, baseDelay, steps: 4);

        // assert (harsh profile last shard should have larger delay than mild profile last shard)
        harsh[^1][0].Should().BeGreaterThan(mild[^1][0]);
    }

    [Fact]
    public void Determinism_ShuffleStable_Deterministic_ForSameSeed()
    {
        // arrange
        var det1 = Determinism.Create(42);
        var det2 = Determinism.Create(42);
        var items1 = new[] { 1, 2, 3, 4, 5 };
        var items2 = new[] { 1, 2, 3, 4, 5 };

        // act
        var arr1 = det1.ShuffleStable(items1);
        var arr2 = det2.ShuffleStable(items2);

        // assert
        arr1.Should().Equal(arr2);
    }
}